using AuthMastery.API.Enums;
using Microsoft.Extensions.Configuration;

namespace AuthMastery.API.Extensions;

/// <summary>
/// Extension methods for safely reading configuration values.
/// These methods assume configuration has been validated at startup,
/// but provide defensive coding with meaningful error messages.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Safely gets an integer configuration value.
    /// Throws InvalidOperationException if value is missing or invalid (should not happen after validation).
    /// </summary>
    public static int GetInt32(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{key}' is missing or empty. This should have been caught during startup validation.");
        }

        if (!int.TryParse(value, out var result) || result <= 0)
        {
            throw new InvalidOperationException($"Configuration value '{key}' is invalid: '{value}'. Expected a positive integer. This should have been caught during startup validation.");
        }

        return result;
    }

    /// <summary>
    /// Safely gets a double configuration value.
    /// Throws InvalidOperationException if value is missing or invalid (should not happen after validation).
    /// </summary>
    public static double GetDouble(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{key}' is missing or empty. This should have been caught during startup validation.");
        }

        if (!double.TryParse(value, out var result) || result <= 0)
        {
            throw new InvalidOperationException($"Configuration value '{key}' is invalid: '{value}'. Expected a positive number. This should have been caught during startup validation.");
        }

        return result;
    }
}

