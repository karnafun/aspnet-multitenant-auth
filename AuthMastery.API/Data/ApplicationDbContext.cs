using AuthMastery.API.Models;
using AuthMastery.API.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthMastery.API.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly ITenantProvider _tenantProvider;
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenantProvider)
       : base(options)
        {
            _tenantProvider = tenantProvider;
        }
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens{ get; set; }
        public DbSet<Tenant> Tenants{ get; set; }
        public DbSet<AuditLog> AuditLogs{ get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectTag> ProjectTags { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ProjectWatcher> ProjectWatchers{ get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            #region ApplicationUser
            modelBuilder.Entity<ApplicationUser>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => new { u.Email, u.TenantId })
                .IsUnique();

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Tenant)
                .WithMany()
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.ProjectsWatching)
                .WithOne(pw => pw.User)
                .HasForeignKey(pw => pw.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            
            modelBuilder.Entity<ApplicationUser>()
                .HasQueryFilter(u => u.TenantId== _tenantProvider.GetTenantId() && !u.IsDeleted);

            #endregion


            #region RefreshToken
            modelBuilder.Entity<RefreshToken>()
                .HasKey(rt => rt.Id);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.Token)
                .IsUnique();
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.UserId);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.ApplicationUser)       
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            #endregion


            #region Tenant
            modelBuilder.Entity<Tenant>()
                .HasKey(t => t.Id);
            modelBuilder.Entity<Tenant>()
                .HasIndex(t => t.Identifier)
                .IsUnique();
            modelBuilder.Entity<Tenant>()
                .HasQueryFilter(t => !t.IsDeleted);
            #endregion

            #region Project
            modelBuilder.Entity<Project>()
               .HasKey(p => p.Id);
            modelBuilder.Entity<Project>()
                .HasIndex(p => p.TenantId);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.Tenant)
                .WithMany()
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Project>()
                .HasOne(p=>p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Project>()
                .HasOne(p => p.AssignedTo)
                .WithMany()
                .HasForeignKey(p => p.AssignedToId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.ProjectTags)
                .WithOne(pt=>pt.Project)
                .HasForeignKey(pt => pt.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.ProjectWatchers)
                .WithOne(pw => pw.Project)
                .HasForeignKey(pw => pw.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
               .HasQueryFilter(p => p.TenantId == _tenantProvider.GetTenantId());
            #endregion
           
            #region ProjectTag
            modelBuilder.Entity<ProjectTag>()
               .HasKey(pt => pt.Id);

            modelBuilder.Entity<ProjectTag>()
                .HasIndex(pt => pt.ProjectId);
            modelBuilder.Entity<ProjectTag>()
             .HasIndex(pt => pt.TagId);

            modelBuilder.Entity<ProjectTag>()
               .HasQueryFilter(pt => pt.Project.TenantId == _tenantProvider.GetTenantId());
            #endregion

            #region Tag
            modelBuilder.Entity<Tag>()
                .HasKey(t => t.Id);
            modelBuilder.Entity<Tag>()
                .HasIndex(t => t.TenantId);
            modelBuilder.Entity<Tag>()
                .HasMany(t => t.ProjectTags)
                .WithOne(pt => pt.Tag)
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Tag>()
                .HasOne(t => t.Tenant)
                .WithMany()
                .HasForeignKey(t => t.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Tag>()
               .HasQueryFilter(t => t.TenantId== _tenantProvider.GetTenantId());
            #endregion

            #region ProjectWatcher

            modelBuilder.Entity<ProjectWatcher>()
                .HasKey(pw => pw.Id);


            modelBuilder.Entity<ProjectWatcher>()
                .HasIndex(pw => new { pw.UserId, pw.ProjectId })
                .IsUnique();

            modelBuilder.Entity<ProjectWatcher>()
             .HasQueryFilter(t => t.Project.TenantId == _tenantProvider.GetTenantId());
            #endregion

        }
    }
}