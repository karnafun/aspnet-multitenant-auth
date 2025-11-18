namespace AuthMastery.API.Models
{
    public class ProjectWatcher
    {
        public int Id { get; set; }
        public required Guid ProjectId { get; set; }
        public Project Project { get; set; }
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }
}
