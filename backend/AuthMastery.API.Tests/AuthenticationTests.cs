using AuthMastery.API.DTO.Auth;
using AuthMastery.API.Models;
using AuthMastery.API.Tests.DTO;
using AuthMastery.API.Tests;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using System.Net;
using System;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using AuthMastery.API.Data;
using AuthMastery.API.Enums;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Azure;
using System.Reflection.PortableExecutable;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AuthMastery.API.DTO.Project;

[Collection("Database")]
public class AuthenticationTests :
    IClassFixture<AuthTestFactory>,
    IClassFixture<TestDataFixture>
{
    private readonly TestDataFixture _testData;
    private readonly HttpClient _client;

    public AuthenticationTests(TestDataFixture testData)
    {
        _testData = testData;
        _client = testData.Factory.CreateClient();
    }

    [Fact]
    public async Task FullLogin_ValidCredentials_ReturnsTokensAndAccessesProtectedEndpoint()
    {
        // Arrange
        var user = _testData.Tenant1TestUsers.First();
        var loginRequest = new LoginRequestDto
        {
            Email = user.Email,
            Password = user.Password,
            TenantIdentifier = user.TenantIdentifier
        };

        // Act - Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert - Login succeeded
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        tokens.Should().NotBeNull();

        // Act - Access protected endpoint
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        meRequest.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");
        var meResponse = await _client.SendAsync(meRequest);

        // Assert - Protected endpoint returns correct user data
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var userData = await meResponse.Content.ReadFromJsonAsync<UserInfoResponseDto>();
        userData.Should().NotBeNull();
        userData.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task TenantIsolation_UserCannotAccessOtherTenantsData()
    {
        // Arrange
        var tenant1User = _testData.Tenant1TestUsers.First();
        var tenant2User = _testData.Tenant2TestUsers.First();

        var loginRequest = new LoginRequestDto
        {
            Email = tenant1User.Email,
            Password = tenant1User.Password,
            TenantIdentifier = tenant1User.TenantIdentifier
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        // Act & Assert - Can access own tenant user
        var ownUserRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admins/users/{tenant1User.Email}");
        ownUserRequest.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");
        var ownUserResponse = await _client.SendAsync(ownUserRequest);

        ownUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var userData = await ownUserResponse.Content.ReadFromJsonAsync<UserAdminDetailsDto>();
        userData.Should().NotBeNull();
        userData.Email.Should().Be(tenant1User.Email);
        userData.TenantId.Should().Be(tenant1User.TenantId);

        // Act & Assert - Cannot access other tenant user
        var otherTenantRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admins/users/{tenant2User.Email}");
        otherTenantRequest.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");
        var otherTenantResponse = await _client.SendAsync(otherTenantRequest);

        otherTenantResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Act & Assert - Get all users returns only own tenant
        var allUsersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admins/users/");
        allUsersRequest.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");
        var allUsersResponse = await _client.SendAsync(allUsersRequest);

        allUsersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allUsers = await allUsersResponse.Content.ReadFromJsonAsync<List<UserAdminDto>>();
        allUsers.Should().NotBeNull();
        allUsers.Count.Should().Be(_testData.Tenant1TestUsers.Count());
    }

    [Fact]
    public async Task RefreshToken_ReuseAfterGracePeriod_DetectsTheftAndRevokesAllTokens()
    {
        // Arrange
        var user = _testData.Tenant1TestUsers.First();
        var loginRequest = new LoginRequestDto
        {
            Email = user.Email,
            Password = user.Password,
            TenantIdentifier = user.TenantIdentifier
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var originalRefreshToken = TestingUtils.GetRefreshTokenFromResponse(loginResponse);

        // Act - First refresh (legitimate)
        var firstRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        firstRefreshRequest.Headers.Add("Cookie", $"refreshToken={originalRefreshToken}");
        var firstRefreshResponse = await _client.SendAsync(firstRefreshRequest);

        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshData = await firstRefreshResponse.Content.ReadFromJsonAsync<RefreshResponseDto>();
        refreshData.Should().NotBeNull();
        refreshData.AccessToken.Should().NotBeNull();
        var newRefreshToken = TestingUtils.GetRefreshTokenFromResponse(firstRefreshResponse);

        // Act - Reuse within grace period (allowed)
        var gracePeriodReuseRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        gracePeriodReuseRequest.Headers.Add("Cookie", $"refreshToken={originalRefreshToken}");
        var gracePeriodReuseResponse = await _client.SendAsync(gracePeriodReuseRequest);

        gracePeriodReuseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait out grace period
        Thread.Sleep(5000);

        // Act - Reuse after grace period (theft detected)
        var theftAttemptRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        theftAttemptRequest.Headers.Add("Cookie", $"refreshToken={originalRefreshToken}");
        var theftAttemptResponse = await _client.SendAsync(theftAttemptRequest);

        theftAttemptResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert - New refresh token is also revoked (all tokens revoked)
        var newTokenRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        newTokenRequest.Headers.Add("Cookie", $"refreshToken={newRefreshToken}");
        var newTokenResponse = await _client.SendAsync(newTokenRequest);

        newTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert - Audit log contains theft detection
        using var scope = _testData.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditLog = await context.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(al => al.UserId == user.Id && al.Details.Contains("Token reuse detected - possible theft"));

        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = _testData.Tenant1TestUsers.First();
        var loginRequest = new LoginRequestDto
        {
            Email = user.Email,
            Password = "WrongPassword123!",
            TenantIdentifier = user.TenantIdentifier
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentEmail_ReturnsUnauthorized()
    {
        // Arrange
        var user = _testData.Tenant1TestUsers.First();
        var loginRequest = new LoginRequestDto
        {
            Email = "nonexistent@email.com",
            Password = user.Password,
            TenantIdentifier = user.TenantIdentifier
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InvalidTenant_ReturnsUnauthorized()
    {
        // Arrange
        var user = _testData.Tenant1TestUsers.First();
        var loginRequest = new LoginRequestDto
        {
            Email = user.Email,
            Password = user.Password,
            TenantIdentifier = "nonexistent-tenant"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesAllRefreshTokens()
    {
        // Arrange
        var user = _testData.Tenant1TestUsers.First();
        var loginRequest = new LoginRequestDto
        {
            Email = user.Email,
            Password = user.Password,
            TenantIdentifier = user.TenantIdentifier
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        var refreshToken = TestingUtils.GetRefreshTokenFromResponse(loginResponse);

        // Act - Logout
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/revoke");
        logoutRequest.Headers.Add("Authorization", $"Bearer {tokens.AccessToken}");
        logoutRequest.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        var logoutResponse = await _client.SendAsync(logoutRequest);

        // Assert - Logout succeeded
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - Refresh token no longer works
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        var refreshResponse = await _client.SendAsync(refreshRequest);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert - All tokens revoked in database
        using var scope = _testData.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activeTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .CountAsync();

        activeTokens.Should().Be(0);
    }

    [Fact]
    public async Task Login_MissingEmail_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new LoginRequestDto
        {
            Email = null,
            Password = "Password123!",
            TenantIdentifier = "tenant1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_MissingTenantIdentifier_ReturnsBadRequest()
    {
        // Arrange
        var user = _testData.Tenant1TestUsers.First();
        var loginRequest = new LoginRequestDto
        {
            Email = user.Email,
            Password = user.Password,
            TenantIdentifier = null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}