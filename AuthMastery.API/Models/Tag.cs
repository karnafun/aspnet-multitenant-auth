namespace AuthMastery.API.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public required string Name{ get; set; }
        public required string Slug { get; set; } 
        public ICollection<ProjectTag> ProjectTags { get; set; }
        public required int TenantId { get; set; }
        public Tenant Tenant { get; set; }
    }
}
