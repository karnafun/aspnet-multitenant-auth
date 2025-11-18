using AuthMastery.API.Data;
using AuthMastery.API.DTO.Auth;
using AuthMastery.API.Enums;
using AuthMastery.API.Extensions;
using AuthMastery.API.Models;
using AuthMastery.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthMastery.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;    
    public AuthController(
        AuthService authService,
        IConfiguration configuration,
        ILogger<AuthController> logger  )
    {
        _configuration = configuration;
        _authService = authService;
        _logger = logger;        
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke() {
        _logger.LogInformation("Revoke endpoint called");
        var refreshToken = Request.Cookies[AuthConstants.RefreshTokenCookie];
        await _authService.RevokeRefreshTokenAsync(refreshToken);
        Response.Cookies.Delete(AuthConstants.RefreshTokenCookie);
        return Ok();
    }
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh() {
        _logger.LogInformation("Refresh endpoint called");

        var refreshToken = Request.Cookies[AuthConstants.RefreshTokenCookie];
        var result = await _authService.RefreshAccessTokenAsync(refreshToken);
        if (!result.IsSuccess)
            return Unauthorized(new { error = result.ErrorMessage });

        Response.Cookies.Append(AuthConstants.RefreshTokenCookie, result.RefreshToken!, GetRefreshTokenCookieOptions());
        return Ok(new
        {
            accessToken = result.AccessToken,
            expiresIn = _configuration[ConfigurationKeys.AccessTokenExpirationMinutes]!
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {    
        _logger.LogInformation("Login endpoint called");

        var result = await _authService.LoginAsync(request.Email, request.Password, request.TenantIdentifier);

        if (!result.IsSuccess)
            return Unauthorized(new { error = result.ErrorMessage });

        Response.Cookies.Append(AuthConstants.RefreshTokenCookie, result.RefreshToken!, GetRefreshTokenCookieOptions());
        
        return Ok(new LoginResponseDto
        {
            AccessToken = result.AccessToken!,
            TokenType = "Bearer",
            ExpiresIn = int.Parse(_configuration[ConfigurationKeys.AccessTokenExpirationMinutes]!) * 60 // 15 minutes in seconds
        });
    }

    [Authorize]  
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User.GetUserId();
        var email = User.GetEmail();
        var tenantId = User.GetTenantId();  
        return Ok(new
        {
            userId,
            email,
            tenantId
        });
    }


    private CookieOptions GetRefreshTokenCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(
                double.Parse(_configuration[ConfigurationKeys.RefreshTokenExpirationDays]!))
        };
    }

}