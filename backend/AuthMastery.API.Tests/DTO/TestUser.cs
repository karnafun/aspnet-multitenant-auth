using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthMastery.API.Tests.DTO
{
    public class TestUser
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string TenantIdentifier { get; set; }
        public int TenantId { get; set; }

    }
}
