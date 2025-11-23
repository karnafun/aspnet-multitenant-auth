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
using Microsoft.Identity.Client;
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
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                throw new ProjectOperationException(tenantId: tenantId, projectId, inner: ex);
            }

        }
        public async Task<List<ProjectListDto>> GetAllProjectsAsync()
        {
            var tenantId = _tenantProvider.GetTenantId();

            _logger.LogInformation("Fetching all projects for TenantId: {TenantId}", tenantId);

            try
            {
                var projects = await _context.Projects
                    .AsNoTracking()
                    .Select(p => new ProjectListDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Status = p.Status.ToString(),
                        CreatedByName = p.CreatedBy.UserName,
                        WatcherCount = p.ProjectWatchers.Count,
                        AssignedTo = p.AssignedTo.Email
                    })                    
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} projects for TenantId: {TenantId}", projects.Count, tenantId);
                return projects;

            }
            catch (Exception ex)
            {
                throw new ProjectOperationException(tenantId: tenantId, inner: ex);
            }
        }
        public async Task DeleteProjectAsync(Guid projectId)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("Deleting project {ProjectId} for TenantId: {TenantId}", projectId, tenantId);
            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null) return;

                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Project {ProjectId} deleted", projectId);
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                throw new ProjectOperationException(tenantId: tenantId, projectId, inner: ex);
            }
        }
        public async Task UpdateProjectAsync(Guid projectId, UpdateProjectDto dto)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("Update project {ProjectId} for TenantId: {TenantId}", projectId, tenantId);
            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null)
                    throw new NotFoundException($"Project {projectId} not found");

                if (!string.IsNullOrEmpty(dto.AssignedTo))
                {

                    var assignedTo = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Email == dto.AssignedTo.Trim());
                    if (assignedTo == null)
                        throw new NotFoundException($"User {dto.AssignedTo} not found");

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
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new
                {
                    dto.Title,
                    dto.Description,
                    dto.Status,
                    dto.AssignedTo
                };
                throw new ProjectOperationException(tenantId: tenantId, projectId, context, inner: ex);
            }
        }
        public async Task AddTagAsync(Guid projectId, string tagSlug)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("Adding tag {TagId} to project {ProjectId} for tenant: {TenantId} ", tagSlug, projectId, tenantId);

            try
            {
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
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new
                {
                    tagSlug
                };
                throw new ProjectOperationException(tenantId: tenantId, projectId, context, inner: ex);

            }

        }
        public async Task RemoveTagAsync(Guid projectId, string tagSlug)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("Removing tag {TagId} from project {ProjectId} for tenant {TenantId}", tagSlug, projectId, tenantId);
            try
            {


                var projectTag = await _context.ProjectTags
                    .Include(pt => pt.Tag)
                    .Where(pt => pt.Tag.Slug == tagSlug && pt.ProjectId == projectId)
                    .FirstOrDefaultAsync();

                if (projectTag == null)
                {
                    return;
                }

                _context.ProjectTags.Remove(projectTag);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tag {TagSlug} was removed from project {ProjectId} ", tagSlug, projectId);
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new
                {
                    tagSlug
                };
                throw new ProjectOperationException(tenantId: tenantId, projectId, context, inner: ex);
            }
        }
        public async Task AddWatcherAsync(Guid projectId, string watcherEmail)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation(
                "Adding watcher {WatcherEmail} to project {ProjectId} for tenant {TenantId}",
                watcherEmail, projectId, tenantId
            );

            try
            {
                var watcher = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == watcherEmail);

                if (watcher == null)
                    throw new NotFoundException($"User {watcherEmail} not found");

                var exists = await _context.ProjectWatchers
                    .AnyAsync(pw => pw.ProjectId == projectId && pw.UserId == watcher.Id);

                if (exists) return;


                var projectExists = await _context.Projects
                    .AnyAsync(p => p.Id == projectId);

                if (!projectExists)
                    throw new NotFoundException($"Project {projectId} not found");

                _context.ProjectWatchers.Add(new ProjectWatcher
                {
                    ProjectId = projectId,
                    UserId = watcher.Id
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Watcher {WatcherEmail} was added to project {ProjectId} for tenant {TenantId}",
                    watcherEmail, projectId, tenantId
                );
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new { watcherEmail };
                throw new ProjectOperationException(tenantId: tenantId, projectId, context, inner: ex);
            }
        }
        public async Task RemoveWatcherAsync(Guid projectId, string watcherEmail)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation(
                "Removing watcher {WatcherEmail} from project {ProjectId} for tenant {TenantId}",
                watcherEmail, projectId, tenantId
            );

            try
            {
                // Load project and watcher in one step via join
                var projectWatcher = await _context.ProjectWatchers
                    .Include(pw => pw.Project)
                    .Include(pw => pw.User)
                    .Where(pw => pw.ProjectId == projectId && pw.User.Email == watcherEmail && pw.Project.TenantId == tenantId)
                    .FirstOrDefaultAsync();

                if (projectWatcher == null)
                {
                    return;
                }

                _context.ProjectWatchers.Remove(projectWatcher);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Watcher {WatcherEmail} was removed from project {ProjectId} for tenant {TenantId}",
                    watcherEmail, projectId, tenantId
                );
            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new { watcherEmail };
                throw new ProjectOperationException(tenantId: tenantId, projectId, context, inner: ex);
            }
        }
        public async Task<ProjectDetailDto> CreateProjectAsync(CreateProjectDto dto, string creatorEmail)
        {
            var tenantId = _tenantProvider.GetTenantId();
            _logger.LogInformation("Creating new project for TenantId: {TenantId}", tenantId);

            try
            {
                var creator = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == creatorEmail);

                if (creator == null)
                    throw new NotFoundException($"Creator '{creatorEmail}' not found.");

                ApplicationUser? assignedTo = null;
                if (!string.IsNullOrEmpty(dto.AssignedToEmail))
                {
                    assignedTo = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Email == dto.AssignedToEmail);

                    if (assignedTo == null)
                        throw new NotFoundException($"AssignedTo user '{dto.AssignedToEmail}' not found.");
                }

                var tags = dto.TagsSlugs?.Any() == true
                    ? await _context.Tags
                        .AsNoTracking()
                        .Where(t => dto.TagsSlugs.Contains(t.Slug))
                        .ToListAsync()
                    : new List<Tag>();

                var missingTags = dto.TagsSlugs?.Except(tags.Select(t => t.Slug)).ToList();
                if (missingTags?.Any() == true)
                    throw new NotFoundException($"Cannot add tags, missing: {string.Join(", ", missingTags)}");

                var watchers = dto.WatchersEmails?.Any() == true
                    ? await _context.Users
                        .AsNoTracking()
                        .Where(u => dto.WatchersEmails.Contains(u.Email!))
                        .ToListAsync()
                    : new List<ApplicationUser>();

                var missingWatchers = dto.WatchersEmails?.Except(watchers.Select(u => u.Email)).ToList();
                if (missingWatchers?.Any() == true)
                    throw new NotFoundException($"Cannot add watchers, missing: {string.Join(", ", missingWatchers)}");

                var project = new Project
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    Status = dto.Status ?? ProjectStatus.OPEN,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = creator.Id,
                    AssignedToId = assignedTo?.Id,
                    ProjectTags = new List<ProjectTag>(),
                };

                await _context.Projects.AddAsync(project);
                await _context.SaveChangesAsync();

                if (tags.Any())
                {
                    _context.ProjectTags.AddRange(tags.Select(tag => new ProjectTag
                    {
                        ProjectId = project.Id,
                        TagId = tag.Id
                    }));
                }

                if (watchers.Any())
                {
                    _context.ProjectWatchers.AddRange(watchers.Select(watcher => new ProjectWatcher
                    {
                        ProjectId = project.Id,
                        UserId = watcher.Id
                    }));
                }

                if (tags.Any() || watchers.Any())
                    await _context.SaveChangesAsync();

                _logger.LogInformation("Project '{ProjectTitle}' created by {CreatorEmail} for TenantId {TenantId}",
                    project.Title, creatorEmail, tenantId);


                var projectWithRelations = await _context.Projects
                    .Include(p => p.ProjectTags).ThenInclude(pt => pt.Tag)
                    .Include(p => p.ProjectWatchers).ThenInclude(pw => pw.User)
                    .Include(p=>p.CreatedBy)
                    .FirstAsync(p => p.Id == project.Id);

                return _mapper.Map<ProjectDetailDto>(projectWithRelations);

            }
            catch (Exception ex) when (ex is not (NotFoundException or BadRequestException))
            {
                var context = new
                {
                    dto.Title,
                    dto.Description,
                    dto.Status,
                    dto.AssignedToEmail,
                    dto.TagsSlugs,
                    dto.WatchersEmails
                };

                throw new ProjectOperationException(tenantId: tenantId, context: context, inner: ex);
            }
        }

        public List<ProjectStatusDto> GetStatusList()
        {
            var statuses = Enum.GetValues(typeof(ProjectStatus))
                 .Cast<ProjectStatus>()
                 .Select(s => new ProjectStatusDto
                 {
                     Value = (int)s,
                     Name = s.ToString(),
                     DisplayName = Utils.GetDisplayName(s)
                 });

            return statuses.ToList();
        }
    }
}
