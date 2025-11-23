using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AuthMastery.API.Tests
{
    public static class TestingUtils
    {
        public static JwtSecurityToken DecodeJwt(string jwtToken)
        {
            var handler = new JwtSecurityTokenHandler();

            // Check if the token is a valid JWT format
            if (!handler.CanReadToken(jwtToken))
            {
                throw new Exception("Cannot read jwt token");
            }

            // Read the JWT token
            var token = handler.ReadJwtToken(jwtToken);

            // Access header information
            Console.WriteLine("--- JWT Header ---");
            foreach (var headerClaim in token.Header)
            {
                Console.WriteLine($"{headerClaim.Key}: {headerClaim.Value}");
            }

            // Access payload claims
            Console.WriteLine("\n--- JWT Payload Claims ---");
            foreach (var claim in token.Claims)
            {
                Console.WriteLine($"{claim.Type}: {claim.Value}");
            }
            return token;
        }
        public static string GetRefreshTokenFromResponse(HttpResponseMessage response) {

            var cookies = response.Headers.GetValues("Set-Cookie");
            var refreshTokenCookie = cookies.First(c => c.StartsWith("refreshToken="));
            var refreshToken = refreshTokenCookie.Split(';')[0].Split('=')[1];
            return Uri.UnescapeDataString(refreshToken);
        }
    
    }


}
