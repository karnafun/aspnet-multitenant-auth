using AuthMastery.API.Enums;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AuthMastery.API.Extensions;

/// <summary>
/// Extension methods for validating application configuration on startup.
/// Ensures all required configuration values are present before the application starts.
/// </summary>
public static class ConfigurationValidationExtensions
{
    /// <summary>
    /// Validates that all required configuration values are present.
    /// Throws InvalidOperationException if any required values are missing.
    /// </summary>
    public static void ValidateRequiredConfiguration(this IConfiguration configuration)
    {
        var missingConfigs = new List<string>();

        // Database connection
        var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            missingConfigs.Add("SQL_CONNECTION_STRING or ConnectionStrings:DefaultConnection");
        }

        // JWT Configuration
        var jwtSecret = configuration[ConfigurationKeys.JwtSecret];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            missingConfigs.Add(ConfigurationKeys.JwtSecret);
        }
        else if (jwtSecret.Length < 32)
        {
            Log.Warning("JWT Secret is less than 32 characters. This may not be secure for HMAC-SHA256.");
        }

        var refreshTokenSecret = configuration[ConfigurationKeys.RefreshTokenSecret];
        if (string.IsNullOrWhiteSpace(refreshTokenSecret))
        {
            missingConfigs.Add(ConfigurationKeys.RefreshTokenSecret);
        }
        else if (refreshTokenSecret.Length < 32)
        {
            Log.Warning("Refresh Token Secret is less than 32 characters. This may not be secure.");
        }

        var jwtIssuer = configuration[ConfigurationKeys.JwtIssuer];
        if (string.IsNullOrWhiteSpace(jwtIssuer))
        {
            missingConfigs.Add(ConfigurationKeys.JwtIssuer);
        }

        var jwtAudience = configuration[ConfigurationKeys.JwtAudience];
        if (string.IsNullOrWhiteSpace(jwtAudience))
        {
            missingConfigs.Add(ConfigurationKeys.JwtAudience);
        }

        // Validate numeric configurations
        if (!int.TryParse(configuration[ConfigurationKeys.AccessTokenExpirationMinutes], out var accessTokenExp) || accessTokenExp <= 0)
        {
            missingConfigs.Add($"{ConfigurationKeys.AccessTokenExpirationMinutes} (must be a positive integer)");
        }

        if (!double.TryParse(configuration[ConfigurationKeys.RefreshTokenExpirationDays], out var refreshTokenExp) || refreshTokenExp <= 0)
        {
            missingConfigs.Add($"{ConfigurationKeys.RefreshTokenExpirationDays} (must be a positive number)");
        }

        if (!int.TryParse(configuration[ConfigurationKeys.GracePeriodInSeconds], out var gracePeriod) || gracePeriod < 0)
        {
            missingConfigs.Add($"{ConfigurationKeys.GracePeriodInSeconds} (must be a non-negative integer)");
        }

        // HTTPS Certificate Configuration (optional for development, required for production)
        var certPath = configuration["Https:CertificatePath"];
        var certPassword = configuration["Https:CertificatePassword"];

        if (string.IsNullOrWhiteSpace(certPath) && string.IsNullOrWhiteSpace(certPassword))
        {
            Log.Information("HTTPS certificate configuration not found. Using default development certificate.");
        }

        if (missingConfigs.Any())
        {
            var errorMessage = $"Missing or invalid required configuration values:\n{string.Join("\n", missingConfigs.Select(c => $"  - {c}"))}";
            Log.Error(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        Log.Information("✅ All required configuration values validated successfully");
    }
}

