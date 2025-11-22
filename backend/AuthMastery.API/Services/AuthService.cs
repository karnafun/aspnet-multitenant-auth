using AuthMastery.API.Data;
using AuthMastery.API.DTO;
using AuthMastery.API.Enums;
using AuthMastery.API.Extensions;
using AuthMastery.API.Models;
using AuthMastery.API.Services.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace AuthMastery.API.Services;

public class AuthService
{
    private readonly ApplicationDbContext _context;
    private readonly TokenService _tokenService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;    
    public AuthService(
        ApplicationDbContext context,
        TokenService tokenService,
        UserManager<ApplicationUser> userManager, 
        ILogger<AuthService> logger,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _context = context;
        _tokenService = tokenService;
        _userManager = userManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;        

    }

    public async Task RevokeRefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogDebug("Revoke called with no token - user already logged out");
            return;
        }

        try
        {
            var hashedToken = _tokenService.HashToken(refreshToken);
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.ApplicationUser)
                .FirstOrDefaultAsync(rt => rt.Token == hashedToken);

            if (storedToken == null)
            {
                _logger.LogDebug("Revoke called with non-existent token - user already logged out");
                return;
            }

            if (storedToken.IsRevoked)
            {
                _logger.LogDebug("Token already revoked for user {UserId} - idempotent logout",
                    storedToken.UserId);
                return;
            }

            if (storedToken.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogDebug("Revoke called on expired token for user {UserId} - user already logged out",
                    storedToken.UserId);
                return;
            }

            if (storedToken.IsUsed)
            {
                _logger.LogDebug("Revoke called on used token for user {UserId}", storedToken.UserId);
                return;
            }

            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;

            _logger.LogInformation("User {UserId} logged out - refresh token revoked",
                storedToken.UserId);

            _context.AuditLogs.Add(new AuditLog
            {
                Action = AuditActions.TokenRevoke,
                Success = true,
                UserId = storedToken.UserId,
                TenantId = storedToken.ApplicationUser.TenantId,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Details = "User logged out",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke refresh token - continuing with logout");
        }
    }
    public async Task<RefreshResult> RefreshAccessTokenAsync(string? refreshToken)
    {
        _logger.LogInformation("Refresh token request received");

        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("Refresh token missing in request");
            return RefreshResult.Failed("Invalid refresh token");
        }

        try
        {
            var hashedIncomingToken = _tokenService.HashToken(refreshToken);
            var storedToken = await _context.RefreshTokens
                .IgnoreQueryFilters()
                .Include(rt => rt.ApplicationUser)
                .FirstOrDefaultAsync(rt => rt.Token == hashedIncomingToken);

            if (storedToken == null)
            {
                _logger.LogWarning("Refresh token not found in database");
                return RefreshResult.Failed("Invalid refresh token");
            }

            _logger.LogInformation("Processing refresh token for user {UserId} in tenant {TenantId}",
                storedToken.UserId, storedToken.ApplicationUser.TenantId);

            if (storedToken.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired refresh token used by user {UserId}", storedToken.UserId);

                _context.AuditLogs.Add(new AuditLog
                {
                    Action = AuditActions.TokenRefresh,
                    Success = false,
                    UserId = storedToken.ApplicationUser.Id,
                    TenantId = storedToken.ApplicationUser.TenantId,
                    IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    Details = "Expired refresh token",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return RefreshResult.Failed("Invalid refresh token");
            }

            if (storedToken.IsRevoked)
            {
                _logger.LogWarning("Attempted use of revoked refresh token by user {UserId}", storedToken.UserId);

                _context.AuditLogs.Add(new AuditLog
                {
                    Action = AuditActions.TokenRefresh,
                    Success = false,
                    UserId = storedToken.ApplicationUser.Id,
                    TenantId = storedToken.ApplicationUser.TenantId,
                    IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    Details = "Revoked refresh token used",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return RefreshResult.Failed("Invalid refresh token");
            }

            if (storedToken.IsUsed)
            {
                if (storedToken.UsedAt == null)
                {
                    _logger.LogError("Data corruption: Token marked as used but UsedAt is null for user {UserId}",
                        storedToken.UserId);
                    return RefreshResult.Failed("Invalid refresh token");
                }
                var timeSinceUsed = DateTime.UtcNow - storedToken.UsedAt;
                var gracePeriodMs = _configuration.GetInt32(ConfigurationKeys.GracePeriodInSeconds) * 1000;
                if (timeSinceUsed.Value.TotalMilliseconds > gracePeriodMs)
                {
                    _logger.LogWarning("SECURITY: Token reuse detected for user {UserId} - possible theft",
                        storedToken.UserId);
                    // TODO: Send security alert email
                    // await _emailService.SendSecurityAlertAsync(
                    //     storedToken.ApplicationUser.Email,
                    //     "Suspicious Activity Detected",
                    //     "We detected unusual activity on your account..."
                    // );
                    var storedTokens = await _context.RefreshTokens.Where(rt => rt.UserId == storedToken.UserId).ToListAsync();
                    storedTokens.ForEach(rt =>
                    {
                        rt.IsRevoked = true;
                        rt.RevokedAt = DateTime.UtcNow;
                    });
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Action = AuditActions.TokenTheftDetection,
                        Success = false,
                        UserId = storedToken.ApplicationUser.Id,
                        TenantId = storedToken.ApplicationUser.TenantId,
                        IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                        Details = "Token reuse detected - possible theft",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                    return RefreshResult.Failed("Invalid refresh token");
                }
            }

            var accessToken = await _tokenService.GenerateAccessToken(storedToken.ApplicationUser);

            storedToken.IsUsed = true;
            storedToken.UsedAt = DateTime.UtcNow;
            var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();

            var newRefreshToken = new RefreshToken
            {
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_configuration.GetDouble(ConfigurationKeys.RefreshTokenExpirationDays)),
                IsRevoked = false,
                IsUsed = false,
                Token = hashedToken,
                UserId = storedToken.ApplicationUser.Id,
            };
            await _context.AddAsync(newRefreshToken);

            _logger.LogInformation("Refresh token rotated, Access token refreshed successfully for user {UserId}", storedToken.UserId);

            _context.AuditLogs.Add(new AuditLog
            {
                Action = AuditActions.TokenRefresh,
                Success = true,
                UserId = storedToken.ApplicationUser.Id,
                TenantId = storedToken.ApplicationUser.TenantId,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Details = "Token refreshed successfully",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return RefreshResult.Success(accessToken, rawToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshAccessTokenAsync failed ");
            throw;  
        }
    }
    public async Task<LoginResult> LoginAsync(string email, string password, string tenantIdentifier)
    {
        _logger.LogInformation("Login attempt for {Email} in tenant {TenantIdentifier}",
         email, tenantIdentifier);

        try
        {
            var tenant = await _context.Tenants
               .FirstOrDefaultAsync(t => t.Identifier == tenantIdentifier);

            if (tenant == null)
                return LoginResult.Failed("invalid credentials");

            var user = await _context.Users
                .IgnoreQueryFilters() // Bypass AUTOMATIC filter
                .FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenant.Id); // EXPLICIT filter

            if (user == null || !await _userManager.CheckPasswordAsync(user, password))
            {
                _logger.LogWarning("Failed login attempt for {Email} in tenant {TenantIdentifier} - invalid credentials",
                email, tenantIdentifier);
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = AuditActions.Login,
                    Success = false,
                    UserId = user?.Id ?? "unknown",
                    TenantId = tenant.Id,
                    IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                    Details = "Invalid credentials",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return LoginResult.Failed("Invalid credentials");
            }

            _logger.LogInformation("User {UserId} from tenant {TenantId} authenticated successfully",
            user.Id, user.TenantId);

            var accessToken = await _tokenService.GenerateAccessToken(user);
            var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
            var refreshToken = new RefreshToken
            {
                Token = hashedToken,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_configuration.GetDouble(ConfigurationKeys.RefreshTokenExpirationDays)),
                IsRevoked = false,
                IsUsed = false
            };
            _context.RefreshTokens.Add(refreshToken);
            _context.AuditLogs.Add(new AuditLog
            {
                Action = AuditActions.Login,
                Success = true,
                UserId = user.Id,
                TenantId = user.TenantId,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Details = "Login successful",
                Timestamp = DateTime.UtcNow
            });

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Login completed for user {UserId} - tokens issued", user.Id);
            return LoginResult.Success(accessToken, rawToken, user);
        }
        catch (Exception ex )
        {
            _logger.LogError(ex, "LoginAsync failed for user {Email}",email);
            throw;
        }
    }
}

