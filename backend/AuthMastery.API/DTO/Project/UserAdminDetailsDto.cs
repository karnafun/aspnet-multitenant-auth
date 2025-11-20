using AuthMastery.API.Models;

namespace AuthMastery.API.DTO.Project
{
    public class UserAdminDetailsDto
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public DateTime LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public required int TenantId { get; set; }
        public ICollection<ProjectListDto> ProjectsWatching { get; set; } = [];
    }
}
