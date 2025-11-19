using AuthMastery.API.Enums;
using AuthMastery.API.Models;

namespace AuthMastery.API.DTO.Project
{
    public class ProjectDetailDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public ProjectStatus Status { get; set; }
        public UserDto CreatedBy { get; set; }
        public UserDto? AssignedTo { get; set; }
        public List<TagDto> Tags { get; set; }
        public List<UserDto> Watchers { get; set; }
    }
}
