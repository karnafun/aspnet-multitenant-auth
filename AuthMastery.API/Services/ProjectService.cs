using AuthMastery.API.Data;
using AuthMastery.API.DTO;
using AuthMastery.API.DTO.Project;
using AuthMastery.API.Enums;
using AuthMastery.API.Models;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthMastery.API.Services
{
    public class ProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly TokenService _tokenService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProjectService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ITenantProvider _tenantProvider;
        private readonly IMapper _mapper;
        public ProjectService(
            ApplicationDbContext context,
            TokenService tokenService,
            UserManager<ApplicationUser> userManager,
            ILogger<ProjectService> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ITenantProvider tenantProvider,
            IMapper mapper)
        {
            _context = context;
            _tokenService = tokenService;
            _userManager = userManager;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _tenantProvider = tenantProvider;
            _mapper = mapper;


        }

        public async Task<ProjectDetailDto> GetProjectByIdAsync(Guid projectId)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("Fetching project {ProjectId} for TenantId: {TenantId}", projectId, tenantId);
            try
            {
                var project = await _context.Projects
                    .Include(p => p.CreatedBy)
                    .Include(p => p.AssignedTo)
                    .Include(p => p.ProjectTags)
                    .ThenInclude(pt => pt.Tag)
                    .Include(p => p.ProjectWatchers).ThenInclude(pw => pw.User)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                
                if (project == null)
                    throw new NotFoundException($"Project {projectId} not found");

                return _mapper.Map<ProjectDetailDto>(project);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching project {ProjectId} for TenantId: {TenantId}", projectId, tenantId);
                throw;
            }

        }
        public async Task<List<ProjectListDto>> GetAllProjectsAsync()
        {
            var tenantId = _tenantProvider.GetTenantId();

            _logger.LogInformation("Fetching all projects for TenantId: {TenantId}", tenantId);

            try
            {
                var projects = await _context.Projects
                    .Include(p => p.CreatedBy)
                    .Include(p => p.AssignedTo)
                    .Include(p => p.ProjectTags)
                        .ThenInclude(pt => pt.Tag)
                    .Include(p => p.ProjectWatchers)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} projects for TenantId: {TenantId}", projects.Count, tenantId);

                return projects.Select(p => new ProjectListDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Status = p.Status.ToString(),
                    CreatedByName = p.CreatedBy?.UserName ?? "Unknown",
                    WatcherCount = p.ProjectWatchers.Count
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching projects for TenantId: {TenantId}", tenantId);
                throw;
            }
        }

        public async Task DeleteProjectAsync(Guid projectId)
        {

            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null)
                    throw new NotFoundException($"Project {projectId} not found");

                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Project {ProjectId} deleted", projectId);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Error in GetAllProjectsAsync");
                throw;
            }

        }

        public async Task UpdateProjectAsync(Guid projectId, UpdateProjectDto dto)
        {
            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null)
                    throw new NotFoundException($"Project {projectId} not found");

                if (!string.IsNullOrEmpty(dto.AssignedTo))
                {

                    var assignedTo = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.AssignedTo.ToString());
                    if (assignedTo == null)
                        throw new BadRequestException($"User {dto.AssignedTo} not found");

                    project.AssignedToId = assignedTo.Id;
                }
                else
                {
                    project.AssignedToId = null;
                }

                project.Title = dto.Title;

                project.Status = dto.Status;
                project.Description = dto.Description;


                await _context.SaveChangesAsync();
                _logger.LogInformation("Project {ProjectId} updated", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateProjectAsync for project {ProjectId} with UpdateProjectDto: {UpdateProjectDto}",projectId,JsonSerializer.Serialize(dto));
                throw;
            }

        }

        public async Task AddTagAsync(Guid projectId, string tagSlug) {
            try
            {
                _logger.LogInformation("Adding tag {TagId} to project {ProjectId} ", tagSlug, projectId);

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null)
                    throw new NotFoundException($"Project {project} not found");


                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Slug == tagSlug);
                if (tag == null)
                    throw new NotFoundException($"Tag {tagSlug} not found");


                var exists = await _context.ProjectTags
                    .AnyAsync(pt => pt.ProjectId == projectId && pt.Tag.Slug == tagSlug);
                if (exists) return;


                var projectTag = new ProjectTag()
                {
                    ProjectId = project.Id,
                    TagId = tag.Id,
                };
                _context.ProjectTags.Add(projectTag);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tag {TagSlug} was added to project {ProjectId} ", tagSlug, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddTagAsync for {ProjectId}, tag {TagSlug}", projectId,tagSlug);
                throw;
            }

        }

        public async Task RemoveTagAsync(Guid projectId, string tagSlug)
        {
            try
            {
                _logger.LogInformation("Removing tag {TagId} from project {ProjectId} ", tagSlug, projectId);

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null)
                    throw new NotFoundException($"Project {project} not found");

                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Slug == tagSlug);
                if (tag == null)
                    throw new NotFoundException($"Tag {tagSlug} not found");


                var projectTag = await _context.ProjectTags.FirstOrDefaultAsync(pt => pt.ProjectId == project.Id && pt.TagId == tag.Id);
                if (projectTag == null)
                    return; // Already removed - idempotent

                _context.ProjectTags.Remove(projectTag);
                await _context.SaveChangesAsync();


                _logger.LogInformation("Tag {TagSlug} was removed from project {ProjectId} ", tagSlug, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveTagAsync for {ProjectId}, tag {TagSlug}", projectId, tagSlug);
                throw;
            }
        }

        public async Task AddWatcherAsync(Guid projectId, string watcherEmail)
        {
            _logger.LogInformation("Adding watcher {watcherEmail} to project {ProjectId} ", watcherEmail, projectId);
            try
            {

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null)
                    throw new NotFoundException($"Project {project} not found");


                var watcher = await _context.Users.FirstOrDefaultAsync(u => u.Email == watcherEmail);
                if (watcher == null)
                    throw new NotFoundException($"User {watcherEmail} not found");


                var exists = await _context.ProjectWatchers
                    .AnyAsync(pw => pw.ProjectId == projectId && pw.UserId == watcher.Id);
                if (exists) return;


                var projectWatcher = new ProjectWatcher()
                {
                    ProjectId = project.Id,
                    UserId = watcher.Id
                };
                _context.ProjectWatchers.Add(projectWatcher);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Watcher {WatcherEmail} was added to project {ProjectId} ", watcherEmail, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddWatcherAsync for {ProjectId}, tag {WatcherEmail}", projectId, watcherEmail);
                throw;
            }

        }

        public async Task RemoveWatcherAsync(Guid projectId, string watcherEmail)
        {
            _logger.LogInformation("Removing watcher {WatcherEmail} from project {ProjectId} ", watcherEmail, projectId);

            try
            {
                var project = await _context.Projects
                      .FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null)
                    throw new NotFoundException($"Project {project} not found");

                var watcher = await _context.Users.FirstOrDefaultAsync(u => u.Email == watcherEmail);
                if (watcher == null)
                    throw new NotFoundException($"Watcher {watcherEmail} not found");

                var projectWatcher = await _context.ProjectWatchers.FirstOrDefaultAsync(pw => pw.ProjectId == project.Id && pw.UserId == watcher.Id);
                if (projectWatcher == null)
                    return; // Already removed - idempotent

                _context.ProjectWatchers.Remove(projectWatcher);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Watcher {WatcherEmail} was removed from project {ProjectId} ", watcherEmail, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveWatcherAsync for {ProjectId}, tag {WatcherEmail}", projectId, watcherEmail);
                throw;
            }
        }

    }
}
