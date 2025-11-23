using AuthMastery.API.Enums;
using AuthMastery.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace AuthMastery.API.Data;

public class DbSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DbSeeder> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor; 
    private readonly RoleManager<IdentityRole> _roleManager; 

    public DbSeeder(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<DbSeeder> logger, IHttpContextAccessor httpContextAccessor, RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _roleManager = roleManager;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting database seeding...");
        await SeedRolesAsync();
        // Seed Tenants (no tenant context needed)
        var tenants = GetSeedTenants();
        foreach (var tenant in tenants)
        {
            if (!await _context.Tenants.AnyAsync(t => t.Identifier == tenant.Identifier))
            {
                _context.Tenants.Add(tenant);
                _logger.LogInformation("Creating tenant: {TenantName}", tenant.Name);
            }
        }
        await _context.SaveChangesAsync();

        // Reload tenants
        var tenant1 = await _context.Tenants.FirstAsync(t => t.Identifier == "acme");
        var tenant2 = await _context.Tenants.FirstAsync(t => t.Identifier == "widgets");

        // Seed data for each tenant
        await SeedTenantDataAsync(tenant1.Id);
        await SeedTenantDataAsync(tenant2.Id);

        _logger.LogInformation("✅ Database seeded successfully!");
    }

    private async Task SeedTenantDataAsync(int tenantId)
    {
        _logger.LogInformation("Seeding data for TenantId: {TenantId}", tenantId);

        // Set tenant context
        SetSeederTenantContext(tenantId);

        // Seed Users
        var users = GetSeedUsers(tenantId);

        foreach (var (email, userName, password) in users)
        {
            if (await FindUserByEmailAsync(email) == null)
            {
                await CreateUser(email, userName, password, tenantId, email ==users.FirstOrDefault().Email);
                _logger.LogInformation("Creating user: {Email}", email);
            }
        }

        // Seed Tags
        var tags = GetSeedTags(tenantId);
        foreach (var tag in tags)
        {
            if (!await _context.Tags.AnyAsync(t => t.Name == tag.Name && t.TenantId == tag.TenantId))
            {
                _context.Tags.Add(tag);
            }
        }
        await _context.SaveChangesAsync();

        // Reload tags
        var tagsFromDb = await _context.Tags.Where(t => t.TenantId == tenantId).ToListAsync();

        // Seed Projects
        var projects = await GetSeedProjects(tenantId);
        foreach (var project in projects)
        {
            if (!await _context.Projects.AnyAsync(p => p.Title == project.Title && p.TenantId == project.TenantId))
            {
                _context.Projects.Add(project);
                _logger.LogInformation("Creating project: {Title}", project.Title);
            }
        }
        await _context.SaveChangesAsync();

        // Reload projects
        var projectsFromDb = await _context.Projects.Where(p => p.TenantId == tenantId).ToListAsync();

        // Seed ProjectTags
        var projectTags = GetSeedProjectTags(projectsFromDb, tagsFromDb);
        foreach (var projectTag in projectTags)
        {
            if (!await _context.ProjectTags.AnyAsync(pt => pt.ProjectId == projectTag.ProjectId && pt.TagId == projectTag.TagId))
            {
                _context.ProjectTags.Add(projectTag);
            }
        }
        await _context.SaveChangesAsync();

        // Seed ProjectWatchers
        var projectWatchers = await GetSeedProjectWatchers(projectsFromDb);
        foreach (var watcher in projectWatchers)
        {
            if (!await _context.ProjectWatchers.AnyAsync(pw => pw.ProjectId == watcher.ProjectId && pw.UserId == watcher.UserId))
            {
                _context.ProjectWatchers.Add(watcher);
            }
        }
        await _context.SaveChangesAsync();
    }

    private void SetSeederTenantContext(int tenantId)
    {
        var claims = new List<Claim>
    {
        new Claim(CustomClaimTypes.TenantId, tenantId.ToString())
    };
        var identity = new ClaimsIdentity(claims, "Seeder");
        var principal = new ClaimsPrincipal(identity);

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = principal
        };
    }

    private List<(string Email, string UserName, string Password)> GetSeedUsers(int tenantId)
    {
        // Return different users based on tenantId
        return tenantId switch
        {
            1 => new List<(string, string, string)> // Assuming tenant1.Id = 1 (Acme)
        {
            ("john@acme.com", "John Doe", "Password123!"),
            ("jane@acme.com", "Jane Smith", "Password123!"),
            ("sarah@acme.com", "Sarah Wilson", "Password123!"),
            ("mike@acme.com", "Mike Davis", "Password123!"),
            ("emma@acme.com", "Emma Taylor", "Password123!")
        },
            2 => new List<(string, string, string)> // Assuming tenant2.Id = 2 (Widgets)
        {
            ("bob@widgets.com", "Bob Johnson", "Password123!"),
            ("alice@widgets.com", "Alice Brown", "Password123!"),
            ("charlie@widgets.com", "Charlie Martinez", "Password123!"),
            ("diana@widgets.com", "Diana Anderson", "Password123!"),
            ("frank@widgets.com", "Frank Thomas", "Password123!")
        },
            _ => new List<(string, string, string)>()
        };
    }

    private List<Tag> GetSeedTags(int tenantId)
    {
        return tenantId switch
        {
            1 => new List<Tag>
        {
            new Tag { Name = "High Priority",Slug="high-priority", TenantId = tenantId },
            new Tag { Name = "Low Priority",Slug="low-priority", TenantId = tenantId },
            new Tag { Name = "Marketing", Slug="marketing",TenantId = tenantId },
            new Tag { Name = "R&D", Slug="r&d",TenantId = tenantId },
            new Tag { Name = "Finance", Slug="finance",TenantId = tenantId }
        },
            2 => new List<Tag>
        {
            new Tag { Name = "Urgent",Slug="urgent", TenantId = tenantId },
            new Tag { Name = "Marketing", Slug="marketing",TenantId = tenantId },
            new Tag { Name = "Finance", Slug="finance",TenantId = tenantId },
            new Tag { Name = "Sales", Slug="sales",TenantId = tenantId }
        },
            _ => new List<Tag>()
        };
    }

    private List<Tenant> GetSeedTenants()
    {
        return new List<Tenant>
        {
            new Tenant
            {
                Name = "Acme Corporation",
                Identifier = "acme",
                CreatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Name = "Widgets Inc",
                Identifier = "widgets",
                CreatedAt = DateTime.UtcNow
            }
        };
    }

  

    private async Task<List<Project>> GetSeedProjects(int tenantId)
    {
        var users = await _context.Users.Where(u => u.TenantId == tenantId).ToListAsync();

        return tenantId switch
        {
            1 => new List<Project>
        {
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Website Redesign",
                Description = "Complete overhaul of company website with modern UI/UX",
                TenantId = tenantId,
                CreatedById = users.First(u => u.Email == "john@acme.com").Id,
                AssignedToId = users.First(u => u.Email == "jane@acme.com").Id,
                CreatedAt = DateTime.UtcNow,
                Status = ProjectStatus.IN_PROGRESS
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Q4 Marketing Campaign",
                Description = "Launch new product marketing campaign for Q4",
                TenantId = tenantId,
                CreatedById = users.First(u => u.Email == "jane@acme.com").Id,
                AssignedToId = users.First(u => u.Email == "john@acme.com").Id,
                CreatedAt = DateTime.UtcNow,
                Status = ProjectStatus.OPEN
            }
        },
            2 => new List<Project>
        {
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Inventory System Upgrade",
                Description = "Migrate to new cloud-based inventory management system",
                TenantId = tenantId,
                CreatedById = users.First(u => u.Email == "bob@widgets.com").Id,
                AssignedToId = users.First(u => u.Email == "alice@widgets.com").Id,
                CreatedAt = DateTime.UtcNow,
                Status = ProjectStatus.IN_PROGRESS
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Title = "Sales Dashboard Development",
                Description = "Build real-time sales analytics dashboard",
                TenantId = tenantId,
                CreatedById = users.First(u => u.Email == "alice@widgets.com").Id,
                AssignedToId = users.First(u => u.Email == "bob@widgets.com").Id,
                CreatedAt = DateTime.UtcNow,
                Status = ProjectStatus.OPEN
            }
        },
            _ => new List<Project>()
        };
    }

    private List<ProjectTag> GetSeedProjectTags(List<Project> projects, List<Tag> tags)
    {
        // Match by project title for deterministic seeding
        var project1 = projects.FirstOrDefault(p => p.Title.Contains("Redesign") || p.Title.Contains("Inventory"));
        var project2 = projects.FirstOrDefault(p => p.Title.Contains("Marketing") || p.Title.Contains("Dashboard"));

        if (project1 == null || project2 == null) return new List<ProjectTag>();

        var result = new List<ProjectTag>();

        // For tenant 1 or 2, add appropriate tags
        if (tags.Any(t => t.Name == "High Priority"))
        {
            // Tenant 1 tags
            result.Add(new ProjectTag { ProjectId = project1.Id, TagId = tags.First(t => t.Name == "High Priority").Id });
            result.Add(new ProjectTag { ProjectId = project1.Id, TagId = tags.First(t => t.Name == "Marketing").Id });
            result.Add(new ProjectTag { ProjectId = project2.Id, TagId = tags.First(t => t.Name == "Marketing").Id });
            result.Add(new ProjectTag { ProjectId = project2.Id, TagId = tags.First(t => t.Name == "Low Priority").Id });
        }
        else
        {
            // Tenant 2 tags
            result.Add(new ProjectTag { ProjectId = project1.Id, TagId = tags.First(t => t.Name == "Urgent").Id });
            result.Add(new ProjectTag { ProjectId = project1.Id, TagId = tags.First(t => t.Name == "Finance").Id });
            result.Add(new ProjectTag { ProjectId = project2.Id, TagId = tags.First(t => t.Name == "Sales").Id });
            result.Add(new ProjectTag { ProjectId = project2.Id, TagId = tags.First(t => t.Name == "Marketing").Id });
        }

        return result;
    }

    private async Task<List<ProjectWatcher>> GetSeedProjectWatchers(List<Project> projects)
    {
        var tenantId = projects.First().TenantId;
        var users = await _context.Users.Where(u => u.TenantId == tenantId).ToListAsync();

        var project1 = projects.First();
        var project2 = projects.Last();

        return tenantId switch
        {
            1 => new List<ProjectWatcher>
        {
            new ProjectWatcher { ProjectId = project1.Id, UserId = users.First(u => u.Email == "sarah@acme.com").Id },
            new ProjectWatcher { ProjectId = project1.Id, UserId = users.First(u => u.Email == "mike@acme.com").Id },
            new ProjectWatcher { ProjectId = project2.Id, UserId = users.First(u => u.Email == "sarah@acme.com").Id }
        },
            2 => new List<ProjectWatcher>
        {
            new ProjectWatcher { ProjectId = project1.Id, UserId = users.First(u => u.Email == "charlie@widgets.com").Id },
            new ProjectWatcher { ProjectId = project1.Id, UserId = users.First(u => u.Email == "diana@widgets.com").Id },
            new ProjectWatcher { ProjectId = project2.Id, UserId = users.First(u => u.Email == "charlie@widgets.com").Id }
        },
            _ => new List<ProjectWatcher>()
        };
    }
  

    private async Task<ApplicationUser?> FindUserByEmailAsync(string email)
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    private async Task CreateUser(string email, string userName, string password, int tenantId, bool admin = false)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, password);
        await _userManager.AddToRoleAsync(user, admin ? "Admin" : "User");

        if (!result.Succeeded)
        {
            throw new Exception($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    private async Task SeedRolesAsync()
    {
        var roles = new[] { "Admin", "User" };
        foreach (var roleName in roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole { Name = roleName });
                _logger.LogInformation("Created role: {RoleName}", roleName);
            }
        }
    }
}