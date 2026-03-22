using System.Security.Claims;
using System.Text.Json;

namespace Not.Application.Authentication.User;

public static class NUserClaimsHelper
{
    public static NUserRegistration? ResolveRegistration(ClaimsPrincipal? principal)
    {
        var email = ResolveEmail(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new NUserRegistration(
            email,
            ResolveName(principal),
            ResolveGivenName(principal),
            ResolveSurname(principal),
            ResolveCountryRegion(principal)
        );
    }

    public static string? ResolveUserIdentifier(ClaimsPrincipal? principal)
    {
        return ResolveClaimValue(
            principal,
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier",
            "sub",
            ClaimTypes.NameIdentifier
        );
    }

    public static string? ResolveEmail(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return null;
        }

        var rawEmail =
            principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("emails")?.Value
            ?? principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Upn)?.Value;

        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            return null;
        }

        if (!rawEmail.StartsWith('['))
        {
            return rawEmail;
        }

        try
        {
            var emails = JsonSerializer.Deserialize<string[]>(rawEmail);
            return emails?.FirstOrDefault(email => !string.IsNullOrWhiteSpace(email));
        }
        catch (JsonException)
        {
            return rawEmail;
        }
    }

    public static string? ResolveName(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return null;
        }

        var fullName = ResolveClaimValue(principal, ClaimTypes.Name, "name");
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return Normalize(fullName);
        }

        var parts = new[] { ResolveGivenName(principal), ResolveSurname(principal) }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(" ", parts);
    }

    public static string? ResolveGivenName(ClaimsPrincipal? principal)
    {
        return ResolveClaimValue(principal, ClaimTypes.GivenName, "given_name", "givenName");
    }

    public static string? ResolveSurname(ClaimsPrincipal? principal)
    {
        return ResolveClaimValue(principal, ClaimTypes.Surname, "family_name", "surname", "surName");
    }

    public static string? ResolveCountryRegion(ClaimsPrincipal? principal)
    {
        return ResolveClaimValue(principal, ClaimTypes.Country, "country", "country_region", "country/region", "ctry");
    }

    static string? ResolveClaimValue(ClaimsPrincipal? principal, params string[] claimTypes)
    {
        if (principal == null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = principal
                .Claims.Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
                .Select(claim => Normalize(claim.Value))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
