using AuthMastery.API.Models;

namespace AuthMastery.API.DTO.Project
{
    public class UserDetailsDto
    {
        public DateTime CreatedAt { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}
