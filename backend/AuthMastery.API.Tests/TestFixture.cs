using AuthMastery.API.Data;
using AuthMastery.API.Enums;
using AuthMastery.API.Models;
using AuthMastery.API.Tests.DTO;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace AuthMastery.API.Tests
{
    [CollectionDefinition("Database")]
    public class DatabaseCollection : ICollectionFixture<TestDataFixture>
    {
        // This class is never instantiated
    }
    public class TestDataFixture : IDisposable
    {
        public AuthTestFactory Factory { get; }
        public List<TestUser> Tenant1TestUsers { get; private set; } = new();
        public List<TestUser> Tenant2TestUsers { get; private set; } = new();
        public List<Tag> Tenant1Tags { get; private set; } = new();
        public List<Tag> Tenant2Tags { get; private set; } = new();

        public TestDataFixture()
        {
            Factory = new AuthTestFactory();

            // Seed everything synchronously (constructors can't be async)
            SeedDataAsync().GetAwaiter().GetResult();
        }
        public async Task SeedDataAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create schema
            await context.Database.EnsureCreatedAsync();
            Tenant1TestUsers.Add(await SeedTestUser("admin1@tenant1.com", "Pass123!", "tenant1", "Tenant 1", admin: true));
            Tenant1TestUsers.Add(await SeedTestUser("creator1@tenant1.com", "Pass123!", "tenant1", "Tenant 1", admin: false));
            Tenant1TestUsers.Add(await SeedTestUser("regular1@tenant1.com", "Pass123!", "tenant1", "Tenant 1", admin: false));
            Tenant1Tags = await SeedTestTags(new List<string> { "Bug", "Feature Request", "High Priority" }, Tenant1TestUsers.First().TenantId);

            Tenant2TestUsers.Add(await SeedTestUser("admin2@tenant2.com", "Pass123!", "tenant2", "Tenant 2", admin: true));
            Tenant2TestUsers.Add(await SeedTestUser("creator2@tenant2.com", "Pass123!", "tenant2", "Tenant 2", admin: false));
            Tenant2TestUsers.Add(await SeedTestUser("regular2@tenant2.com", "Pass123!", "tenant2", "Tenant 2", admin: false));
            Tenant2Tags = await SeedTestTags(new List<string> { "Backend", "Frontend", "Database" }, Tenant2TestUsers.First().TenantId);
        }

        private async Task<TestUser> SeedTestUser(string email, string password, string tenantIdentifier, string tenantName, bool admin = false)
        {
            using var scope = Factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

            var tenant = context.Tenants.FirstOrDefault(t => t.Identifier == tenantIdentifier);
            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Name = tenantName,
                    Identifier = tenantIdentifier,
                    CreatedAt = DateTime.UtcNow,
                };
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
            }


            var claims = new List<Claim>
            {
                new Claim(CustomClaimTypes.TenantId, tenant.Id.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestSeeder");
            var principal = new ClaimsPrincipal(identity);
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = principal
            };

            // Create user
            var user = new ApplicationUser
            {
                UserName = email.Split('@').First(),
                Email = email,
                TenantId = tenant.Id
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, admin ? "Admin" : "User");

            return new TestUser()
            {
                Id = user.Id,
                Email = email,
                Password = password,
                TenantId = tenant.Id,
                TenantIdentifier = tenantIdentifier
            };


        }
        private async Task<List<Tag>> SeedTestTags(List<string> tagNames, int tenantId)
        {
            using var scope = Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

            // Set fake tenant context
            var claims = new List<Claim>
        {
            new Claim(CustomClaimTypes.TenantId, tenantId.ToString())
        };
            var identity = new ClaimsIdentity(claims, "TestSeeder");
            var principal = new ClaimsPrincipal(identity);
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = principal
            };

            var tags = new List<Tag>();
            foreach (var tagName in tagNames)
            {
                var slug = tagName.ToLower().Replace(" ", "-");
                var tag = new Tag
                {
                    Name = tagName,
                    Slug = slug,
                    TenantId = tenantId
                };
                context.Tags.Add(tag);
                tags.Add(tag);
            }

            await context.SaveChangesAsync();
            return tags;
        }

        public void Dispose()
        {
            Factory?.Dispose();
        }

    }


}
