using AuthMastery.API.Models;

namespace AuthMastery.API.Services.Results
{
    public class RefreshResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }

        public static RefreshResult Success(string accessToken, string refreshToken)
        {
            return new RefreshResult
            {
                IsSuccess = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };
        }

        public static RefreshResult Failed(string errorMessage)
        {
            return new RefreshResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
