using AuthMastery.API.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthMastery.API.Models
{

    // Projects are hard-deleted (no soft delete)
    // Reasoning: Portfolio project scope. In production, consider soft delete for:
    // - Accidental deletion recovery
    // - Audit trail compliance
    // - GDPR "right to be forgotten" workflows
    public class Project
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Title { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public ICollection<ProjectTag> ProjectTags { get; set; } = new List<ProjectTag>();

        public string? CreatedById { get; set; }

        public ApplicationUser CreatedBy { get; set; }

        public string? AssignedToId { get; set; }

        public ApplicationUser? AssignedTo { get; set; }

        public ICollection<ProjectWatcher> ProjectWatchers { get; set; } = new List<ProjectWatcher>();

        public required DateTime CreatedAt { get; set; }

        public required ProjectStatus Status { get; set; }

        public required int TenantId { get; set; }

        public Tenant Tenant { get; set; }
    }
}
