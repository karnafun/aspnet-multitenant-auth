namespace AuthMastery.API.Models
{
    public class ProjectTag
    {
        public int Id { get; set; }
        public required Guid ProjectId { get; set; }
        public Project Project { get; set; }
        public required int TagId { get; set; }
        public Tag Tag { get; set; }
    }
}
