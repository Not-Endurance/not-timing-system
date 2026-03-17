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

        return new NUserRegistration(email, ResolveName(principal));
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

        var fullName = principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.FindFirst("name")?.Value;
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName.Trim();
        }

        var givenName = principal.FindFirst(ClaimTypes.GivenName)?.Value ?? principal.FindFirst("given_name")?.Value;
        var surname =
            principal.FindFirst(ClaimTypes.Surname)?.Value
            ?? principal.FindFirst("family_name")?.Value
            ?? principal.FindFirst("surname")?.Value;

        var parts = new[] { givenName, surname }.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return parts.Length == 0 ? null : string.Join(" ", parts);
    }
}
