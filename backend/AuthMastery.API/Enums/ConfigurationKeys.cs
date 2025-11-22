namespace AuthMastery.API.Enums
{
    public static class ConfigurationKeys
    {
        public const string JwtSecret = "Jwt:Secret";
        public const string JwtIssuer = "Jwt:Issuer";
        public const string JwtAudience = "Jwt:Audience";
        public const string RefreshTokenSecret = "Jwt:RefreshTokenSecret";
        public const string RefreshTokenExpirationDays = "Jwt:RefreshTokenExpirationDays";
        public const string AccessTokenExpirationMinutes = "Jwt:AccessTokenExpirationMinutes";
        public const string GracePeriodInSeconds = "GracePeriodInSeconds";
        
        // HTTPS Configuration
        public const string HttpsCertificatePath = "Https:CertificatePath";
        public const string HttpsCertificatePassword = "Https:CertificatePassword";
        
        // CORS Configuration
        public const string CorsOrigins = "Cors:Origins";
        
        // Security Configuration
        public const string RefreshTokenByteLength = "Security:RefreshTokenByteLength";
    }
}
