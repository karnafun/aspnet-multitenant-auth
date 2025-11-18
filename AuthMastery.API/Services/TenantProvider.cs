using AuthMastery.API.Data;
using AuthMastery.API.Enums;
using System.IdentityModel.Tokens.Jwt;

namespace AuthMastery.API.Services
{
    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TenantProvider> _logger;

        public TenantProvider(IHttpContextAccessor httpContextAccessor, ILogger<TenantProvider> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public int GetTenantId()
        {
            var tenantIdClaim = _httpContextAccessor.HttpContext?.User
                .FindFirst(CustomClaimTypes.TenantId)?.Value;

            if (string.IsNullOrEmpty(tenantIdClaim))
            {
                _logger.LogWarning("Missing TenantId claim in authenticated request");
                throw new UnauthorizedAccessException("Invalid token: Missing TenantId claim");
            }

            return int.Parse(tenantIdClaim);
        }
    }
}
