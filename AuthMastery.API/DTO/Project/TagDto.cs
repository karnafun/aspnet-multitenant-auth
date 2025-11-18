using AuthMastery.API.Models;

namespace AuthMastery.API.DTO.Project
{
    public class TagDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string Slug { get; set; }
    }
}
