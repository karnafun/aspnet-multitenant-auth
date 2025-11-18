using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace AuthMastery.API.Models
{
    public class ApplicationUser : IdentityUser
    {
        public DateTime LastLoginAt { get; set; }
        public DateTime CreatedAt{ get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public required int TenantId { get; set; }  
        public Tenant Tenant { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
        public ICollection<ProjectWatcher> ProjectsWatching { get; set; } = [];
    }
}
