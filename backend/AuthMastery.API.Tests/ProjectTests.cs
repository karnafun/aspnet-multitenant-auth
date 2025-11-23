using AuthMastery.API.Data;
using AuthMastery.API.DTO.Auth;
using AuthMastery.API.DTO.Project;
using AuthMastery.API.Enums;
using AuthMastery.API.Models;
using AuthMastery.API.Tests;
using AuthMastery.API.Tests.DTO;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;

public class ProjectTests : 
    IClassFixture<AuthTestFactory>,
    IClassFixture<TestDataFixture>
{
    private readonly TestDataFixture _fixture;
    private readonly HttpClient _client;
    private readonly TestDataFixture _testData;



    public ProjectTests(TestDataFixture fixture, TestDataFixture testData)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _testData = testData;

    }



    public Task DisposeAsync() => Task.CompletedTask;

    #region CRUD Tests

    [Fact]
    public async Task CreateProject_AsAuthenticatedUser_ReturnsCreatedProject()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u=>u.Email.Contains("creator"));  // tenant1Creator
        var token = await GetAccessToken(creator);

        var createDto = new CreateProjectDto
        {
            Title = "Test Project",
            Description = "Test Description",
            Status = ProjectStatus.OPEN
        };

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(createDto);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Should().NotBeNull();
        project.Title.Should().Be(createDto.Title);
        project.Description.Should().Be(createDto.Description);
        project.CreatedBy.Email.Should().Be(creator.Email);
    }

    [Fact]
    public async Task GetAllProjects_ReturnsOnlyUserTenantProjects()
    {
        // Arrange
        var tenant1Creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); ;
        var tenant2Creator = _testData.Tenant2TestUsers.First(u => u.Email.Contains("creator")); ;

        // Create project in tenant 1
        var token1 = await GetAccessToken(tenant1Creator);
        await CreateProject(token1, "Tenant 1 Project");

        // Create project in tenant 2
        var token2 = await GetAccessToken(tenant2Creator);
        await CreateProject(token2, "Tenant 2 Project");

        // Act - Get all projects as tenant 1 user
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
        request.Headers.Add("Authorization", $"Bearer {token1}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectListDto>>();
        projects.Should().NotBeNull();
        projects.All(p => p.Title != "Tenant 2 Project").Should().BeTrue();
        
    }

    [Fact]
    public async Task GetProjectById_DifferentTenant_Returns404()
    {
        // Arrange
        var tenant1Creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator"));
        var tenant2User = _testData.Tenant2TestUsers.First(u => u.Email.Contains("regular")); ;

        // Create project in tenant 1
        var token1 = await GetAccessToken(tenant1Creator);
        var projectId = await CreateProject(token1, "Tenant 1 Project");

        // Act - Try to access as tenant 2 user
        var token2 = await GetAccessToken(tenant2User);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {token2}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_AsAdmin_Succeeds()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var admin = _testData.Tenant1TestUsers.First(u => u.Email.Contains("admin")); // tenant1Admin

        var creatorToken = await GetAccessToken(creator);
        var projectId = await CreateProject(creatorToken, "Project to Delete");

        // Act - Delete as admin
        var adminToken = await GetAccessToken(admin);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {adminToken}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify project is deleted
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}");
        getRequest.Headers.Add("Authorization", $"Bearer {adminToken}");
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_AsNonAdmin_Returns403()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var regular = _testData.Tenant1TestUsers.First(u => u.Email.Contains("regular")); // tenant1Regular

        var creatorToken = await GetAccessToken(creator);
        var projectId = await CreateProject(creatorToken, "Project to Delete");

        // Act - Try to delete as non-admin
        var regularToken = await GetAccessToken(regular);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {regularToken}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Update Authorization Tests

    [Fact]
    public async Task UpdateProject_AsCreator_Succeeds()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var token = await GetAccessToken(creator);
        var projectId = await CreateProject(token, "Original Title");

        var updateDto = new UpdateProjectDto
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Status = ProjectStatus.IN_PROGRESS
        };

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(updateDto);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify update
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}");
        getRequest.Headers.Add("Authorization", $"Bearer {token}");
        var getResponse = await _client.SendAsync(getRequest);
        var project = await getResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Title.Should().Be("Updated Title");
        project.Status.Should().Be(ProjectStatus.IN_PROGRESS);
    }

    [Fact]
    public async Task UpdateProject_AsAdmin_Succeeds()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var admin = _testData.Tenant1TestUsers.First(u => u.Email.Contains("admin")); // tenant1Admin

        var creatorToken = await GetAccessToken(creator);
        var projectId = await CreateProject(creatorToken, "Original Title");

        var updateDto = new UpdateProjectDto
        {
            Title = "Admin Updated Title",
            Description = "Admin Updated",
            Status = ProjectStatus.CLOSED
        };

        // Act - Update as admin
        var adminToken = await GetAccessToken(admin);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {adminToken}");
        request.Content = JsonContent.Create(updateDto);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateProject_AsNonCreatorNonAdmin_Returns403()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var regular = _testData.Tenant1TestUsers.First(u => u.Email.Contains("regular")); // tenant1Regular

        var creatorToken = await GetAccessToken(creator);
        var projectId = await CreateProject(creatorToken, "Original Title");

        var updateDto = new UpdateProjectDto
        {
            Title = "Unauthorized Update",
            Description = "Should fail",
            Status = ProjectStatus.IN_PROGRESS
        };

        // Act - Try to update as regular user (not creator, not admin)
        var regularToken = await GetAccessToken(regular);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{projectId}");
        request.Headers.Add("Authorization", $"Bearer {regularToken}");
        request.Content = JsonContent.Create(updateDto);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Tags Tests

    [Fact]
    public async Task AddTag_AsCreator_Succeeds()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var token = await GetAccessToken(creator);
        var projectId = await CreateProject(token, "Project with Tags");
        var tagSlug = _testData.Tenant1Tags.First().Slug;

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/tags/{tagSlug}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify tag was added
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}");
        getRequest.Headers.Add("Authorization", $"Bearer {token}");
        var getResponse = await _client.SendAsync(getRequest);
        var project = await getResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Tags.Should().Contain(t => t.Slug == tagSlug);
    }

    [Fact]
    public async Task AddTag_AsNonCreatorNonAdmin_Returns403()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var regular = _testData.Tenant1TestUsers.First(u => u.Email.Contains("regular")); // tenant1Regular

        var creatorToken = await GetAccessToken(creator);
        var projectId = await CreateProject(creatorToken, "Project");
        var tagSlug = _testData.Tenant1Tags.First().Slug;

        // Act - Try to add tag as regular user
        var regularToken = await GetAccessToken(regular);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/tags/{tagSlug}");
        request.Headers.Add("Authorization", $"Bearer {regularToken}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddTag_DifferentTenantTag_Returns404()
    {
        // Arrange
        var tenant1Creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator"));
        var token = await GetAccessToken(tenant1Creator);
        var projectId = await CreateProject(token, "Tenant 1 Project");
        var tenant2TagSlug =  _testData.Tenant2Tags.First().Slug; // Tag from different tenant

        // Act - Try to add tenant 2 tag to tenant 1 project
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/tags/{tenant2TagSlug}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveTag_AsCreator_Succeeds()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var token = await GetAccessToken(creator);
        var tagSlug = _testData.Tenant1Tags.First().Slug;

        var createDto = new CreateProjectDto
        {
            Title = "Project with Tag",
            TagsSlugs = new List<string> { tagSlug }
        };

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/projects");
        createRequest.Headers.Add("Authorization", $"Bearer {token}");
        createRequest.Content = JsonContent.Create(createDto);
        var createResponse = await _client.SendAsync(createRequest);
        var project = await createResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

        // Act - Remove the tag
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{project.Id}/tags/{tagSlug}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify tag was removed
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{project.Id}");
        getRequest.Headers.Add("Authorization", $"Bearer {token}");
        var getResponse = await _client.SendAsync(getRequest);
        var updatedProject = await getResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();
        updatedProject.Tags.Should().NotContain(t => t.Slug == tagSlug);
    }

    #endregion

    #region Watchers Tests

    [Fact]
    public async Task AddWatcher_AsCreator_Succeeds()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var watcherUser = _testData.Tenant1TestUsers.First(u => u.Email.Contains("regular")); // tenant1Regular
        var token = await GetAccessToken(creator);
        var projectId = await CreateProject(token, "Project with Watchers");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/watchers/{watcherUser.Email}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify watcher was added
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{projectId}");
        getRequest.Headers.Add("Authorization", $"Bearer {token}");
        var getResponse = await _client.SendAsync(getRequest);
        var project = await getResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();
        project.Watchers.Should().Contain(w => w.Email == watcherUser.Email);
    }

    [Fact]
    public async Task AddWatcher_DifferentTenantUser_Returns404()
    {
        // Arrange
        var tenant1Creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator"));
        var tenant2User = _testData.Tenant2TestUsers.First(u => u.Email.Contains("regular")); // Different tenant
        var token = await GetAccessToken(tenant1Creator);
        var projectId = await CreateProject(token, "Tenant 1 Project");

        // Act - Try to add tenant 2 user as watcher to tenant 1 project
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/watchers/{tenant2User.Email}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveWatcher_AsCreator_Succeeds()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var watcherUser = _testData.Tenant1TestUsers.First(u => u.Email.Contains("regular")); // tenant1Regular
        var token = await GetAccessToken(creator);

        var createDto = new CreateProjectDto
        {
            Title = "Project with Watcher",
            WatchersEmails = new List<string> { watcherUser.Email }
        };

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/projects");
        createRequest.Headers.Add("Authorization", $"Bearer {token}");
        createRequest.Content = JsonContent.Create(createDto);
        var createResponse = await _client.SendAsync(createRequest);
        var project = await createResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

        // Act - Remove the watcher
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{project.Id}/watchers/{watcherUser.Email}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify watcher was removed
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/projects/{project.Id}");
        getRequest.Headers.Add("Authorization", $"Bearer {token}");
        var getResponse = await _client.SendAsync(getRequest);
        var updatedProject = await getResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();
        updatedProject.Watchers.Should().NotContain(w => w.Email == watcherUser.Email);
    }

    [Fact]
    public async Task AddWatcher_AsNonCreatorNonAdmin_Returns403()
    {
        // Arrange
        var creator = _testData.Tenant1TestUsers.First(u => u.Email.Contains("creator")); // tenant1Creator
        var regular = _testData.Tenant1TestUsers.First(u => u.Email.Contains("regular")); // tenant1Regular
        var admin = _testData.Tenant1TestUsers.First(u => u.Email.Contains("admin")); // tenant1Admin (will be the watcher)

        var creatorToken = await GetAccessToken(creator);
        var projectId = await CreateProject(creatorToken, "Project");

        // Act - Try to add watcher as regular user (not creator, not admin)
        var regularToken = await GetAccessToken(regular);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/watchers/{admin.Email}");
        request.Headers.Add("Authorization", $"Bearer {regularToken}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Helper Methods

    private async Task<string> GetAccessToken(TestUser user)
    {
        var loginRequest = new LoginRequestDto
        {
            Email = user.Email,
            Password = user.Password,
            TenantIdentifier = user.TenantIdentifier
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        return loginResponse!.AccessToken;
    }

    private async Task<Guid> CreateProject(string token, string title)
    {
        var createDto = new CreateProjectDto
        {
            Title = title,
            Description = "Test project"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(createDto);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        return project!.Id;
    }



    #endregion
}