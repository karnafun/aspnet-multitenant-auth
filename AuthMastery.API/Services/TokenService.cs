using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthMastery.API.Enums;
using AuthMastery.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.IdentityModel.Tokens;

namespace AuthMastery.API.Services;

public class TokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager; 
    private readonly ILogger<TokenService> _logger;
    public TokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager, ILogger<TokenService> logger)
    {
        _configuration = configuration;        
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<string> GenerateAccessToken(ApplicationUser user)
    {

        try
        {
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(CustomClaimTypes.TenantId, user.TenantId.ToString()),
        };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration[ConfigurationKeys.JwtSecret]!)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(
                    int.Parse(_configuration[ConfigurationKeys.AccessTokenExpirationMinutes]!)
                ),
                SigningCredentials = creds,
                Issuer = _configuration[ConfigurationKeys.JwtIssuer]!,
                Audience = _configuration[ConfigurationKeys.JwtAudience]!
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshAccessTokenAsync failed ");
            throw;
        }
    }

    public (string rawToken, string hashedToken)  GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        string rawToken = Convert.ToBase64String(randomBytes);

        string hashedToken = HashToken(rawToken); 

        return (rawToken, hashedToken);
    }

    public string HashToken(string token)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_configuration[ConfigurationKeys.RefreshTokenSecret]!));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }
}