using AuthMastery.API.Data;
using AuthMastery.API.DTO.Project;
using AuthMastery.API.Enums;
using AuthMastery.API.Extensions;
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
            _logger.LogInformation("GetAllProjects called by user: {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var projects = await _projectService.GetAllProjectsAsync();

            return Ok(projects);
        }

        [Authorize]
        [HttpGet("statuses")]
        public IActionResult GetStatusList() {

            _logger.LogInformation("Get status list called");
            var list = _projectService.GetStatusList();
            return Ok(list);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProjectById(Guid id)
        {
            _logger.LogInformation("GetProjectById called by user: {UserId} for Project {ProjectId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value, id);
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
            id.ValidateGuid(nameof(id));
            
            _logger.LogInformation("Admin {UserId} attempting to delete project {ProjectId}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value, id);
            await _projectService.DeleteProjectAsync(id);
            return NoContent();
        }



        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto project) {
            _logger.LogInformation("User {UserId} attempting to create new project",
                       User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var res = await _projectService.CreateProjectAsync(project, User.FindFirst(ClaimTypes.Email)?.Value);

            return Ok(res);
        }
 
        [Authorize(Policy = "CanManageProjects")]
        [HttpPut("{projectId}")]
        public async Task<IActionResult> UpdateProject(Guid projectId, [FromBody] UpdateProjectDto dto) {
            projectId.ValidateGuid(nameof(projectId));

            _logger.LogInformation("User {UserId} attempting to update project {ProjectId}",
                   User.FindFirst(ClaimTypes.NameIdentifier)?.Value, projectId);

            await _projectService.UpdateProjectAsync(projectId, dto);

            return NoContent();
        }
        

        [Authorize(Policy = "CanManageProjects")]
        [HttpPost("{projectId}/tags/{tagSlug}")]
        public async Task<IActionResult> AddTagToProject(Guid projectId, string tagSlug)
        {
            projectId.ValidateGuid(nameof(projectId));
            tagSlug.ValidateTagSlug(nameof(tagSlug));
            
            await _projectService.AddTagAsync(projectId, tagSlug);
            return NoContent();
        }

        [Authorize(Policy = "CanManageProjects")]
        [HttpDelete("{projectId}/tags/{tagSlug}")]
        public async Task<IActionResult> RemoveTagFromProject(Guid projectId, string tagSlug)
        {
            projectId.ValidateGuid(nameof(projectId));
            tagSlug.ValidateTagSlug(nameof(tagSlug));
            
            await _projectService.RemoveTagAsync(projectId, tagSlug);
            return NoContent();
        }

        [Authorize(Policy = "CanManageProjects")]
        [HttpPost("{projectId}/watchers/{watcherEmail}")]
        public async Task<IActionResult> AddWatcherToProject(Guid projectId, string watcherEmail)
        {
            projectId.ValidateGuid(nameof(projectId));
            watcherEmail.ValidateEmail(nameof(watcherEmail));
            
            await _projectService.AddWatcherAsync(projectId, watcherEmail);
            return NoContent();
        }

        [Authorize(Policy = "CanManageProjects")]
        [HttpDelete("{projectId}/watchers/{watcherEmail}")]
        public async Task<IActionResult> RemoveWatcherFromProject(Guid projectId, string watcherEmail)
        {
            projectId.ValidateGuid(nameof(projectId));
            watcherEmail.ValidateEmail(nameof(watcherEmail));
            
            await _projectService.RemoveWatcherAsync(projectId, watcherEmail);
            return NoContent();
        }
    }

}
