using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AuthMastery.API.DTO;

namespace AuthMastery.API.Extensions;

public static class ValidationExtensions
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly Regex TagSlugPattern = new(@"^[a-z0-9-]+$", RegexOptions.Compiled);

    public static void ValidateEmail(this string email, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BadRequestException($"{parameterName} cannot be empty");
        }

        if (email.Length > 254)
        {
            throw new BadRequestException($"{parameterName} exceeds maximum length of 254 characters");
        }

        if (!EmailValidator.IsValid(email))
        {
            throw new BadRequestException($"{parameterName} is not a valid email address");
        }
    }

    public static void ValidateTagSlug(this string tagSlug, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(tagSlug))
        {
            throw new BadRequestException($"{parameterName} cannot be empty");
        }

        if (tagSlug.Length > 50)
        {
            throw new BadRequestException($"{parameterName} exceeds maximum length of 50 characters");
        }

        if (!TagSlugPattern.IsMatch(tagSlug))
        {
            throw new BadRequestException($"{parameterName} must be lowercase alphanumeric with hyphens only");
        }

        if (tagSlug.StartsWith('-') || tagSlug.EndsWith('-'))
        {
            throw new BadRequestException($"{parameterName} cannot start or end with a hyphen");
        }
    }

    public static void ValidateTagName(this string tagName, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new BadRequestException($"{parameterName} cannot be empty");
        }

        var trimmed = tagName.Trim();
        if (trimmed.Length == 0)
        {
            throw new BadRequestException($"{parameterName} cannot be empty or whitespace only");
        }

        if (trimmed.Length > 100)
        {
            throw new BadRequestException($"{parameterName} exceeds maximum length of 100 characters");
        }
    }

    public static void ValidateGuid(this Guid guid, string parameterName)
    {
        if (guid == Guid.Empty)
        {
            throw new BadRequestException($"{parameterName} cannot be empty");
        }
    }
}

