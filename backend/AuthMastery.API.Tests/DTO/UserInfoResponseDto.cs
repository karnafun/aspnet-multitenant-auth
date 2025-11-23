using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AuthMastery.API.Tests.DTO
{
    public class UserInfoResponseDto
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
        [JsonPropertyName("tenantId")]
        public int TenantId { get; set; }
        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; }
    }
}
