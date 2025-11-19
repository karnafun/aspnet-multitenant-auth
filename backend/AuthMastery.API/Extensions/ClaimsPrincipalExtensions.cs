using AuthMastery.API.Enums;
using System.Security.Claims;

namespace AuthMastery.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);

        if (claim == null)
            throw new UnauthorizedAccessException("User ID claim not found");

        return claim.Value;
    }

    public static string GetEmail(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.Email);

        if (claim == null)
            throw new UnauthorizedAccessException("Email claim not found");

        return claim.Value;
    }

    public static string GetTenantId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(CustomClaimTypes.TenantId); // Custom claim type

        if (claim == null)
            throw new UnauthorizedAccessException("Tenant ID claim not found");

        return claim.Value;
    }

   
}