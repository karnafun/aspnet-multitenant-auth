namespace AuthMastery.API.DTO.Auth
{
    public class LoginResponseDto
    {
        public required string AccessToken { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; } 
    }
}
