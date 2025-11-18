using AuthMastery.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace AuthMastery.API.DTO.Project
{
    public class UpdateProjectDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }
        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public ProjectStatus Status { get; set; }
        
        [EmailAddress]
        public string? AssignedTo { get; set; }

    }
}
