# Security Implementation

*This document covers security threats, mitigations, and residual risks. For architecture and design decisions, see [Architecture.md](./Architecture.md). For setup and deployment, see [README.md](../readme.md).*

## Threat Model

### Assets We're Protecting

- User credentials (passwords)
- Authentication tokens (access tokens, refresh tokens)
- Multi-tenant data (prevent cross-tenant access)
- User sessions (prevent hijacking)
- API endpoints (prevent unauthorized access)

### Threat Actors

- **External Attackers** - No system access, attempting remote attacks
- **Malicious Users** - Valid credentials, attempting privilege escalation or cross-tenant access
- **Compromised Dependencies** - Third-party libraries with vulnerabilities (XSS)
- **Database Breach** - Attacker gains read access to database

### Attack Surface

- React SPA (client-side code, localStorage, cookies)
- API endpoints (authentication, data access)
- Database (stored tokens, user data)
- Network layer (HTTPS, token transmission)

---

## Security Threats & Mitigations

### Threat 1: Cross-Tenant Data Access (CRITICAL)

#### Attack Vector

- Malicious user attempts to access another tenant's data by manipulating API requests
- Developer forgets to add tenant filtering to a query
- SQL injection bypasses tenant filtering
- User manually crafts API call with different tenant ID in request body or URL

#### Impact: CRITICAL

- Complete data breach across tenants
- Loss of customer trust and potential customer exodus
- Regulatory violations (GDPR, SOC 2, industry-specific compliance)
- Potential company-ending incident for B2B SaaS
- Legal liability and financial penalties

#### Likelihood

High (without proper safeguards) → Low (with global query filters implemented)

#### Mitigations Implemented
1. **EF Core Global Query Filters** - Automatically inject `WHERE TenantId = <currentTenant>` into all queries at the ORM level
2. **TenantId in JWT Claims** - Tenant identity embedded in token during authentication, cannot be manipulated by client
3. **ITenantProvider Service** - Centralized tenant resolution that validates and extracts TenantId from JWT claims, throws exception if missing or invalid

4. **Code Review Policy** - Any pull request containing `.IgnoreQueryFilters()` receives mandatory security review to validate business justification and ensure manual tenant filtering is present

#### How Attack is Prevented

Global query filters automatically inject `WHERE TenantId = <currentTenant>` into all queries. Query parameters and request body values are ignored - only the TenantId from JWT claims is used. Developer errors cannot bypass this protection. Admin operations requiring cross-tenant access must explicitly use `.IgnoreQueryFilters()` and undergo security review.

*See [Architecture.md](./Architecture.md#4-ef-core-global-query-filters-for-multi-tenant-isolation) for detailed explanation of global query filters, tenant resolution strategy, and code examples.*

#### Residual Risk

- Developer bypasses filters - `.IgnoreQueryFilters()` allows intentional bypass (mitigated by code review policy)
- Raw SQL queries - Bypass EF Core filters entirely (must manually include `WHERE TenantId = @TenantId`)
- Database breach - Direct database access exposes all tenant data (tokens hashed, but data plaintext)
- Authorization logic bugs - If custom authorization checks are implemented incorrectly
- Subdomain takeover - If attacker compromises subdomain with relaxed CORS, could make authenticated requests

#### Testing Performed

- Attempted to query Tenant B data while authenticated as Tenant A user (blocked)
- Verified `.IgnoreQueryFilters()` properly bypasses filter (admin scenarios)
- Confirmed TenantProvider throws exception when TenantId claim missing
- Manual SQL query inspection to verify `WHERE TenantId = X` in all generated queries


### Threat 2: Refresh Token Theft (HIGH)

#### Attack Vector

- Man-in-the-middle attack if HTTPS compromised or improperly configured
- Database breach where attacker gains access to hashed tokens and attempts rainbow table/brute force
- Memory dump from server during token validation
- Network packet capture if TLS is broken or downgraded
- Attacker gains physical access to user's device
- Session hijacking via compromised browser extension

#### Impact: HIGH

- Attacker can impersonate user for up to 7 days (refresh token lifetime)
- Can access all user's data within their tenant
- Can perform state-changing actions on user's behalf
- Legitimate user may not notice until tokens are rotated or expire

#### Likelihood: Low-Medium

- Low: If HTTPS properly configured and database secured
- Medium: If database credentials compromised or TLS misconfigured

#### Mitigations Implemented
1. **Token Rotation (Primary Defense)** - Every refresh operation generates a new refresh token and marks the old token as `IsUsed = true`. Attempting to use a token marked as used triggers theft detection.
2. **Reuse Detection** - When a refresh token marked `IsUsed = true` is submitted:
   - System assumes token theft has occurred
   - ALL refresh tokens for that user are immediately revoked (`IsRevoked = true`)
   - User forced to re-authenticate
   - Security event logged to AuditLogs table
3. **Grace Period (5 seconds)** - Tokens remain valid for 5 seconds after first use to handle legitimate network retries without triggering false-positive theft detection.
4. **HttpOnly Cookies** - Refresh tokens stored with `HttpOnly=true`, `Secure=true`, `SameSite=Strict` flags preventing JavaScript access and CSRF attacks
5. **Token Hashing** - Refresh tokens hashed using HMACSHA256 with secret key before storage. Database breach exposes hashed tokens, not usable plaintext tokens
6. **Frontend Request Queue** - Axios interceptor prevents multiple concurrent refresh calls, reducing false-positive theft detection
7. **Rate Limiting** - Rate limiting enforced on `/auth/refresh` endpoint to prevent brute force refresh token enumeration

*See [Architecture.md](./Architecture.md#3-token-storage-strategy) for detailed explanation of token storage security and cookie configuration.*

#### How Token Rotation Prevents Theft

When a refresh token is used, it's marked as `IsUsed=true` and a new token is generated. If the same token is used again outside the 5-second grace period, the system detects theft and revokes all tokens for that user. The grace period handles legitimate network retries without triggering false positives.

**Attack Scenario - Token Stolen and Reused:**

```
T=0s:   Legitimate user calls /refresh with Token A
        Backend validates Token A (valid, not used)
        Backend marks Token A as IsUsed=true
        Backend generates Token B, returns to user

T=2s:   ATTACKER intercepts Token A (network sniffing, compromised device)
        Attacker calls /refresh with Token A
        Backend checks Token A: IsUsed=true, created 2 seconds ago (within grace period)
        Backend allows refresh (within grace period), generates Token C
        Attacker receives Token C

T=10s:  Legitimate user's access token expires again
        User calls /refresh with Token B
        Backend validates Token B (valid, not used)
        Backend checks parent token (Token A): IsUsed=true, used >5 seconds ago
        Backend detects Token A was used TWICE (outside grace period)

        🚨 THEFT DETECTED 🚨

T=10s:  Backend revokes ALL refresh tokens for user (sets IsRevoked=true)
        Backend logs security event: TokenTheftDetected
        Backend returns 401 Unauthorized
        Frontend logs user out, redirects to login

Result: Both legitimate user AND attacker forced to re-authenticate
        Attack detected and stopped within 10 seconds
```

*See [Architecture.md](./Architecture.md#token-refresh-flow) for detailed sequence diagrams and token rotation flow explanation.*

#### Why 5 Seconds?

Balances security vs usability:
- Too short (1-2s): Slow mobile networks trigger false positives
- Too long (30s+): Gives attacker extended window to use stolen token
- 5 seconds: Handles typical network latency while minimizing attack window

Industry implementations: AWS Cognito (up to 60s), Okta (0-60s configurable, default 30s). Our 5-second choice prioritizes security over convenience.

#### Residual Risk

- 5-second grace period - Attacker has 5-second window to use stolen token before detection (acceptable trade-off)
- Cannot prevent initial theft - Only detect and revoke after theft occurs
- Legitimate user locked out - If attacker refreshes first, legitimate user forced to re-authenticate (correct security posture)
- Database breach with hash cracking - Attacker with database access could attempt to crack HMACSHA256 hashes (computationally expensive, unlikely)
- Memory dump during validation - Plaintext token exists in memory briefly during validation (requires server compromise)
- Physical device access - Attacker with device access can extract cookie and use before rotation (time-limited)

#### Defense in Depth Layers

- HttpOnly cookies - Prevent JavaScript access
- Token hashing - Protect against database breach
- Token rotation - Limit token lifetime to single use
- Reuse detection - Detect theft when token used twice
- Grace period - Prevent false positives
- Rate limiting - Prevent brute force enumeration
- Audit logging - Track all suspicious activity


### Threat 3: Cross-Site Scripting (XSS) (HIGH)

#### Attack Vector

- Malicious JavaScript injected via compromised third-party dependency (npm package)
- Input injection if user-generated content not properly sanitized
- Stored XSS in database if HTML rendering not escaped
- DOM-based XSS via URL parameters
- Compromised browser extension

#### Impact: HIGH

- Can make authenticated API requests on behalf of user
- Can perform state-changing actions (create, update, delete)
- Can access user data visible in DOM
- Cannot exfiltrate HttpOnly cookies (refresh token protected)
- Can steal access token from memory (short-lived, limited damage)

#### Likelihood: Medium

- Common vulnerability type
- Hard to eliminate completely given third-party dependencies
- React provides default protections but not foolproof

#### Mitigations Implemented
1. **HttpOnly Cookies for Refresh Tokens** - 7-day lifetime refresh tokens cannot be accessed by JavaScript, preventing exfiltration even if XSS occurs
2. **Access Tokens in Memory Only** - Stored in React state, never persisted. XSS can read token during session but cannot persist or exfiltrate for later use
3. **React Default XSS Protections** - Automatic escaping of JSX values prevents most injection attacks
4. **Content Security Policy (CSP)** - Blocks inline scripts and restricts resource loading to same origin
5. **X-Content-Type-Options** - Prevents MIME-type sniffing attacks
6. **X-Frame-Options** - Prevents clickjacking by blocking iframe embedding

*See [Architecture.md](./Architecture.md#3-token-storage-strategy) for detailed explanation of why HttpOnly cookies and in-memory storage provide XSS protection.*

#### XSS Attack Mitigation

HttpOnly cookies prevent JavaScript access to refresh tokens. Access tokens in memory can be read during the page session but cannot be persisted or exfiltrated. CSP blocks external script execution. Damage is limited to the current session (15-minute access token lifetime).

#### Residual Risk

- XSS can still act as user - For duration of page session, attacker can make authenticated requests (cannot exfiltrate tokens but can perform actions)
- Access token exposed for 15 minutes - Short lifetime limits damage window
- CSP bypass techniques - Advanced attackers may find CSP bypasses
- Compromised dependencies - If XSS in widely-used library, widespread impact
- Social engineering - User tricked into clicking malicious link that executes script

#### Why This is Acceptable

Traditional CSRF tokens don't solve XSS either. If attacker has script execution, they can read CSRF token from DOM and include in requests. Our approach minimizes damage:

- Cannot steal long-lived credentials
- Cannot persist attack beyond session
- 15-minute access token expiry limits window


### Threat 4: Cross-Site Request Forgery (CSRF) (MEDIUM)

#### Attack Vector

- Attacker tricks user into visiting malicious site
- Malicious site makes request to our API using user's cookies
- Browser automatically attaches cookies to cross-site requests

#### Impact: MEDIUM

- Can trigger state-changing operations if cookies used for authentication
- Cannot read response (same-origin policy)
- Limited to HTTP verbs browser allows cross-origin (GET, POST)

#### Likelihood

Low (with SameSite=Strict) → Very Low (with our implementation)

#### Mitigations Implemented
1. **SameSite=Strict Cookies** - Browser will not send cookies for cross-site requests, preventing CSRF attacks
2. **CORS Configuration** - API only accepts requests from specified origins, blocking cross-site requests at the API level
3. **Authorization Header for Access Tokens** - Access tokens sent in `Authorization: Bearer <token>` header, not cookies. Cross-site JavaScript cannot add custom headers (CORS restriction).

#### CSRF Protection

SameSite=Strict cookies prevent cross-site cookie transmission. CORS restricts API access to specified origins. Access tokens in Authorization headers cannot be set by cross-site JavaScript. State-changing operations use POST/PUT/DELETE, which SameSite=Strict blocks for cross-site requests.

#### Residual Risk

- GET requests changing state - If developers violate REST principles and use GET for state-changing operations, vulnerable to CSRF via top-level navigation (mitigated by code review)
- SameSite browser support - Old browsers may not support SameSite (users on outdated browsers vulnerable)
- Subdomain attacks - If attacker controls subdomain (sub.example.com), same-site but different origin (partially mitigated by CORS)
- Client-side routing confusion - React router may handle some navigation client-side in ways that don't trigger SameSite checks

#### Why This is Low Risk

Multiple layers of defense:
- SameSite=Strict (primary defense)
- CORS restricts origins
- Authorization header (can't be set cross-site)
- REST principles (GET doesn't modify state)


### Threat 5: Access Token Theft (MEDIUM)

#### Attack Vector

- XSS attack extracts token from memory
- Network interception if HTTPS compromised
- Browser DevTools access if user leaves workstation unlocked
- Memory dump from user's browser

#### Impact: MEDIUM

- Attacker can impersonate user for 15 minutes (access token lifetime)
- Limited damage window compared to refresh token theft
- Cannot obtain new access tokens without refresh token

#### Likelihood: Low-Medium

- Requires XSS or physical access to user's device
- Short window for exploitation

#### Mitigations Implemented

1. **Short Token Lifetime (15 minutes)** - Even if stolen, token expires quickly, limiting damage window.
2. **Token Stored in Memory Only** - Not persisted to localStorage/sessionStorage/disk. Lost on page refresh.
3. **Automatic Refresh Flow** - User never manually handles tokens, reducing likelihood of accidental exposure.
4. **HTTPS Required (Secure Flag)** - Token transmission encrypted in transit.

```csharp
Secure = true  // Cookie only sent over HTTPS
```

#### Residual Risk

- 15-minute attack window - Stolen token usable until expiration
- No revocation mechanism - Access tokens are stateless, cannot be revoked before expiry (by design)
- Memory access via XSS - If XSS present, token can be read from memory
- Browser DevTools - If physical access, token visible in React DevTools

#### Why This is Acceptable

Short lifetime means:
- Attacker must use token within 15 minutes
- Attacker must maintain active XSS or physical access
- Damage limited to single user, single tenant, single session

Alternative (longer-lived access tokens) would increase risk without benefit.

### Threat 6: Brute Force Authentication (MEDIUM)

#### Attack Vector

- Automated attempts to guess username/password combinations
- Credential stuffing using leaked password databases
- Dictionary attacks against weak passwords

#### Impact: MEDIUM

- Successful authentication grants full user access
- Can lead to account takeover
- Can be used for further attacks (token theft, data exfiltration)

#### Likelihood

High (without rate limiting) → Low (with rate limiting)

#### Mitigations Implemented
1. **Rate Limiting on /auth/login** - Limits number of login attempts per IP address and per username within time window.
2. **ASP.NET Identity Password Requirements** - Enforces password complexity:
   - Minimum length
   - Requires uppercase, lowercase, digit, special character
   - Prevents common passwords
3. **Secure Password Hashing (Identity Framework)** - Passwords hashed using PBKDF2 with per-user salt. Computationally expensive to crack.
4. **Account Lockout (Identity Framework)** - After N failed attempts, account temporarily locked, requiring password reset.

#### Residual Risk

- Distributed attacks - Per-IP rate limiting can be bypassed with botnet
- Weak user passwords - Despite requirements, users may choose predictable passwords
- No CAPTCHA - Automated tools can still attempt logins (rate limiting provides some protection)
- No 2FA - Single factor authentication vulnerable to credential theft
- Password reuse - Users may reuse passwords leaked from other sites

#### Recommended Improvements

- Implement CAPTCHA after N failed attempts
- Add 2FA for sensitive operations
- Monitor for credential stuffing patterns (same password, different usernames)
- Breach detection integration (check against Have I Been Pwned)


### Threat 7: SQL Injection (LOW - Framework Protected)

#### Attack Vector

- Malicious SQL code injected via user input
- Bypasses parameterization to execute arbitrary queries
- Targets raw SQL queries or poorly constructed dynamic SQL

#### Impact: CRITICAL (if successful)

- Complete database compromise
- Data exfiltration across all tenants
- Data modification or deletion
- Potential for privilege escalation

#### Likelihood: Very Low (with Entity Framework Core)

#### Mitigations Implemented

1. **Entity Framework Core Parameterization** - All LINQ queries automatically parameterized by EF Core:

```csharp
// Developer writes:
var project = await _context.Projects
    .Where(p => p.Name == userInput)
    .FirstOrDefaultAsync();

// EF Core generates:
SELECT * FROM Projects WHERE Name = @p0
-- Parameters: @p0 = 'userInput'
```

User input never concatenated into SQL string.

2. **No Raw SQL Queries** - Codebase uses LINQ exclusively, avoiding `FromSqlRaw` or `ExecuteSqlRaw`.
3. **Input Validation** - Model validation ensures user input conforms to expected types and formats before database access.

#### Residual Risk

- Developer uses `FromSqlRaw` - If developer bypasses LINQ and uses raw SQL without parameterization (mitigated by code review)
- Stored procedures with dynamic SQL - If stored procedures use dynamic SQL construction (none implemented in this project)
- Second-order SQL injection - Malicious data stored in database, later used in dynamic query (theoretical, unlikely with EF Core)

#### Why This is Low Risk

Entity Framework Core's automatic parameterization makes SQL injection extremely unlikely unless developer intentionally bypasses protections.

### Threat 8: Token Replay Attacks (LOW - Mitigated by Rotation)

#### Attack Vector

- Attacker captures valid token
- Replays token at later time to gain unauthorized access

#### Impact: MEDIUM

- Can impersonate user if token still valid
- Scope limited to token lifetime

#### Likelihood: Low (with rotation and short lifetimes)

#### Mitigations Implemented

1. **Access Token Short Lifetime (15 minutes)** - Captured access token has limited replay window.
2. **Refresh Token Rotation** - Each refresh token valid for single use only. Replay attempt triggers theft detection.
3. **HTTPS Required** - Token transmission encrypted, making capture more difficult.

#### Residual Risk

- Captured token usable until expiry - If attacker captures access token, can replay within 15-minute window
- Cannot detect access token replay - Access tokens are stateless; no server-side tracking of usage

#### Why This is Low Risk

Short token lifetimes and refresh token rotation limit replay attack effectiveness. Attacker must capture token and use within small time window.

### Threat 9: Man-in-the-Middle (MITM) (LOW - HTTPS Required)

#### Attack Vector

- Attacker intercepts network traffic between client and server
- Reads or modifies requests/responses in transit
- Steals tokens during transmission

#### Impact: CRITICAL (if successful)

- Can steal access and refresh tokens
- Can read all data transmitted
- Can modify requests to perform unauthorized actions

#### Likelihood: Very Low (with proper HTTPS)

#### Mitigations Implemented

1. **HTTPS Required (Secure Flag)** - All traffic encrypted using TLS.

```csharp
Secure = true  // Cookies only sent over HTTPS
```

2. **HSTS Header** - Strict-Transport-Security header configured via `app.UseHsts()` to force HTTPS connections.

#### Residual Risk

- TLS downgrade attacks - If attacker can force HTTP connection (mitigated by Secure flag)
- Certificate pinning not implemented - Client trusts any valid certificate (uncommon attack)
- Compromised CA - If certificate authority compromised, attacker could issue valid certificate (theoretical)

#### Why This is Low Risk

Modern TLS configuration makes MITM attacks extremely difficult. Proper HTTPS configuration provides strong protection.

---

## Multi-Tenant Isolation Protection

Global query filters prevent cross-tenant access at the application layer. Example:

**Attack Attempt:**
User from Tenant 1 calls: `GET /api/projects?tenantId=2`

**What Happens:**
1. JWT validated, TenantId=1 extracted from claims
2. EF Core injects: `WHERE TenantId = 1`
3. Query parameter `?tenantId=2` ignored
4. User receives only Tenant 1 data

Query string manipulation, request body tampering, and developer errors cannot bypass tenant isolation. Direct database access bypasses these protections and requires database-level security (encryption at rest, access controls, row-level security). Any use of `.IgnoreQueryFilters()` requires security review and proper authorization checks.

*For detailed attack scenarios and code examples, see [Architecture.md](./Architecture.md#4-ef-core-global-query-filters-for-multi-tenant-isolation).*


## Known Limitations & Attack Surface

### What We DON'T Protect Against

1. **No Device Fingerprinting**
   - Cannot detect if refresh token used from different device or IP address
   - Token stolen and used from attacker's device appears identical to legitimate use
   - **Risk:** Silent token theft may go undetected until rotation triggers reuse detection
   - **Mitigation:** Implement device fingerprinting or IP-based anomaly detection for production

2. **No Geographic Anomaly Detection**
   - Token used from different country/region not flagged
   - Attacker using VPN to mimic user's location not detected
   - **Risk:** Account takeover from foreign location may not be noticed
   - **Mitigation:** Add geolocation tracking and alert on unusual locations

3. **No 2FA**
   - Single factor authentication (username/password only)
   - Stolen credentials = full account access
   - **Risk:** Phishing or credential stuffing grants immediate access
   - **Mitigation:** Implement TOTP/SMS 2FA for production

4. **Database Breach = Data Exposure**
   - Tenant data not encrypted at rest in database
   - Direct database access bypasses all application-layer protections
   - Refresh tokens hashed, but all other data plaintext
   - **Risk:** Database compromise exposes all tenant data
   - **Mitigation:** Implement encryption at rest, database access auditing

5. **XSS Can Still Act as User**
   - Cannot exfiltrate tokens, but can make authenticated requests
   - For duration of page session, attacker can perform state-changing operations
   - **Risk:** Compromised dependency can abuse user session
   - **Mitigation:** Strict CSP, dependency auditing, subresource integrity

6. **Compromised TLS = Full Compromise**
   - If HTTPS broken (certificate validation disabled, weak ciphers, etc.), all protections fail
   - **Risk:** MITM attack can steal tokens, read data, modify requests
   - **Mitigation:** Enforce TLS 1.3+, strong ciphers, certificate pinning

7. **No Defense Against Insider Threats**
   - Developer with database access can query all tenant data
   - System administrator can extract tokens from memory/logs
   - **Risk:** Malicious employee can exfiltrate data
   - **Mitigation:** Audit logging, least privilege, separation of duties

8. **No Protection for "Forgot Password" Flow**
   - Not implemented (intentional scope limit)
   - Email-based password reset vulnerable to email account compromise
   - **Risk:** Attacker with email access can reset password and gain access
   - **Mitigation:** Implement secure password reset with rate limiting and email verification

9. **Session Fixation Possible**
   - No explicit session rotation on privilege escalation
   - **Risk:** Attacker tricks user into authenticating with attacker-controlled session ID
   - **Mitigation:** Generate new tokens on role change or privilege escalation


## Recommended Production Improvements

### High Priority

- 2FA/MFA - Add TOTP or SMS-based second factor
- Rate limiting refinement - Per-user, per-IP, and global rate limits with backoff
- Database encryption at rest - Encrypt sensitive data columns (not just tokens)
- Subresource Integrity (SRI) - Verify CDN-loaded resources not tampered with

### Medium Priority

- Device fingerprinting - Track device/browser used for authentication
- Geographic anomaly detection - Flag logins from unusual locations
- IP-based anomaly detection - Detect unusual IP address changes
- Breach detection - Check passwords against Have I Been Pwned API
- Enhanced audit logging - Log all data access, not just authentication events

### Low Priority

- Certificate pinning - Pin expected TLS certificates
- API request signing - Sign requests to prevent tampering
- Biometric authentication - WebAuthn for passwordless login
- Session replay protection - Detect and prevent session replay attacks

---

## Compliance Considerations

### OWASP Top 10 (2021) Coverage
- ✅ **A01 Broken Access Control** - Global query filters, authorization policies
- ⚠️ **A02 Cryptographic Failures** - Tokens/passwords hashed, data not encrypted at rest
- ✅ **A03 Injection** - EF Core parameterization prevents SQL injection
- ✅ **A04 Insecure Design** - Defense in depth, secure defaults
- ✅ **A05 Security Misconfiguration** - Security headers, HTTPS, secure cookies
- ⚠️ **A06 Vulnerable Components** - Latest .NET 8, no automated scanning
- ⚠️ **A07 Auth Failures** - Strong hashing, token rotation, no 2FA
- ⚠️ **A08 Integrity Failures** - No code signing, no SRI
- ✅ **A09 Logging Failures** - Comprehensive audit logging
- ✅ **A10 SSRF** - No outbound requests from user input

## What's NOT Production-Ready

### Critical Gaps

- No 2FA/MFA implementation
- Database not encrypted at rest
- No automated security scanning (SAST/DAST)
- No penetration testing performed
- No incident response plan
- No disaster recovery procedures

### Compliance Requirements Not Met

- **SOC 2** - Missing encryption at rest, incomplete audit logging
- **PCI DSS** - Not compliant (if handling payment data)
- **HIPAA** - Not compliant (if handling health data)
- **GDPR** - Partial compliance (data export/deletion not implemented)

### Operational Security Missing

- No security monitoring/alerting
- No automated vulnerability scanning
- No security update process
- No security training for developers
- No third-party security audit

---

*This security document describes the current implementation. For architecture and design decisions, see [Architecture.md](./Architecture.md).*