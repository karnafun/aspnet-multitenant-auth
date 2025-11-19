using AuthMastery.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AuthMastery.API.DTO.Project
{
    public class CreateProjectDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }
        [MaxLength(1000)]
        public string? Description { get; set; }
        public ProjectStatus? Status { get; set; }
        [EmailAddress]
        [JsonPropertyName("AssignedTo")]
        public string? AssignedToEmail { get; set; } 
        [JsonPropertyName("Tags")]
        public List<string> TagsSlugs { get; set; } = [];
        [JsonPropertyName("Watchers")]
        public List<string> WatchersEmails { get; set; } = [];
    }
}
