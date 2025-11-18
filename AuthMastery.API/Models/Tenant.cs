using System.ComponentModel.DataAnnotations;

namespace AuthMastery.API.Models
{
    public class Tenant
    {
        public int Id { get; set; }
        public required  string Name { get; set; }
        private  string _identifier { get; set; }  
        public required  DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [MaxLength(50)]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Identifier must be lowercase alphanumeric with hyphens only")]
        public required string Identifier
        {
            get => _identifier;
            set => _identifier = value.ToLowerInvariant(); // Force lowercase
        }
    }
}
