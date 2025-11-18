using AuthMastery.API.Data;
using AuthMastery.API.DTO.Project;
using AuthMastery.API.Enums;
using AuthMastery.API.Models;
using AuthMastery.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthMastery.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly AuthService _authService;
        private readonly ProjectService _projectService;
        private readonly ILogger<ProjectsController> _logger;


        public ProjectsController(
            AuthService authService,
            IConfiguration configuration,
            ILogger<ProjectsController> logger,
            ProjectService projectService)
        {
            _configuration = configuration;
            _authService = authService;
            _logger = logger;
            _projectService = projectService;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllProjects()
        {
            _logger.LogInformation("GetAllProjects called by user: {UserId}", User.FindFirst(CustomClaimTypes.Username)?.Value);

            var projects = await _projectService.GetAllProjectsAsync();

            return Ok(projects);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProjectById(Guid id)
        {
            _logger.LogInformation("GetProjectById called by user: {UserId} for Project {ProjectId}", User.FindFirst(CustomClaimTypes.Username)?.Value, id);
            if (id == Guid.Empty)
                return BadRequest();

            var project = await _projectService.GetProjectByIdAsync(id);

            if (project == null)
                return NotFound();

            return Ok(project);

        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            _logger.LogInformation("Admin {UserId} attempting to delete project {ProjectId}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, id);
            await _projectService.DeleteProjectAsync(id);
            return NoContent();
        }

 
        [Authorize(Policy = "CanManageProjects")]
        [HttpPut("{projectId}")]
        public async Task<IActionResult> UpdateProject(Guid projectId, [FromBody] UpdateProjectDto dto) {

            _logger.LogInformation("User {UserId} attempting to update project {ProjectId}",
                   User.FindFirst(ClaimTypes.NameIdentifier)?.Value, projectId);

            await _projectService.UpdateProjectAsync(projectId, dto);

            return NoContent();
        }

        [Authorize(Policy = "CanManageProjects")]
        [HttpPost("{projectId}/tags/{tagSlug}")]
        public async Task<IActionResult> AddTagToProject(Guid projectId, string tagSlug)
        {
            await _projectService.AddTagAsync(projectId, tagSlug);
            return NoContent();
        }

        [Authorize(Policy = "CanManageProjects")]
        [HttpDelete("{projectId}/tags/{tagSlug}")]
        public async Task<IActionResult> RemoveTagFromProject(Guid projectId, string tagSlug)
        {
            await _projectService.RemoveTagAsync(projectId, tagSlug);
            return NoContent();
        }

        [Authorize(Policy = "CanManageProjects")]
        [HttpPost("{projectId}/watchers/{watcherEmail}")]
        public async Task<IActionResult> AddWatcherToProject(Guid projectId, string watcherEmail)
        {
            await _projectService.AddWatcherAsync(projectId, watcherEmail);
            return NoContent();
        }

        [Authorize(Policy = "CanManageProjects")]
        [HttpDelete("{projectId}/watchers/{watcherEmail}")]
        public async Task<IActionResult> RemoveWatcherFromProject(Guid projectId, string watcherEmail)
        {
            await _projectService.RemoveWatcherAsync(projectId, watcherEmail);
            return NoContent();
        }
    }

}
