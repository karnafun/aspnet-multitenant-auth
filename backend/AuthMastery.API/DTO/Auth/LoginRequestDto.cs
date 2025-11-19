using System.ComponentModel.DataAnnotations;

namespace AuthMastery.API.DTO.Auth
{
    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public required string Email { get; set; }
        [Required]
        [MinLength(6)]
        public required string Password { get; set; }
        [Required]
        [MaxLength(50)]
        public required string TenantIdentifier { get; set; }
        
    }
}
