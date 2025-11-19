namespace AuthMastery.API.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int TenantId { get; set; }
        public string Action { get; set; }  // "TokenRevoked", "Login", "PasswordChanged"
        public string Details { get; set; }  // JSON or text: "All tokens revoked due to password change"
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        public bool Success { get; set; } = true; // Default to true

    }
}
