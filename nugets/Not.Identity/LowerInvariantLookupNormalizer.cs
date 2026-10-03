using Microsoft.AspNetCore.Identity;

namespace Not.Identity;

/// <summary>
/// Names and emails are normalized to trimmed lower case, which is how the existing user documents store the email, so
/// the normalized form and the stored one are the same and a lookup needs no second field.
/// </summary>
public sealed class LowerInvariantLookupNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name)
    {
        return Normalize(name);
    }

    public string? NormalizeEmail(string? email)
    {
        return Normalize(email);
    }

    static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }
}
