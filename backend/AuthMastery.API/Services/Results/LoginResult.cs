using AuthMastery.API.Models;

namespace AuthMastery.API.Services.Results
{
    public class LoginResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public ApplicationUser? User { get; set; }

        public static LoginResult Success(string accessToken, string refreshToken, ApplicationUser user)
        {
            return new LoginResult
            {
                IsSuccess = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = user
            };
        }

        public static LoginResult Failed(string errorMessage)
        {
            return new LoginResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
