using AuthMastery.API.Enums;

namespace AuthMastery.API.DTO.Project
{
    public class ProjectListDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string CreatedByName { get; set; }
        public int WatcherCount { get; set; }
        public string AssignedTo { get; set; }
    }
}
