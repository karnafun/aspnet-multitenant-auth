using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AuthMastery.API.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }        
        [MaxLength(64)]
        public required string Token { get; set; }
        public required DateTime ExpiresAt { get; set; }
        public required DateTime CreatedAt{ get; set; }
        public required bool IsUsed { get; set; }
        public DateTime? UsedAt{ get; set; }
        public required bool IsRevoked { get; set; }
        public DateTime? RevokedAt{ get; set; }
        public required string UserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
