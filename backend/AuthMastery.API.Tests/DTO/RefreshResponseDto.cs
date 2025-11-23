using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthMastery.API.Tests.DTO
{
    class RefreshResponseDto
    {
        public required string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}
