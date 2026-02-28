using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Not.Authentication.User;

public class NUserResolver : IUserResolver
{
    public async Task Resolve(TicketReceivedContext context)
    {
        var settings = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationSettings>();
        var claimEnrichers = context.HttpContext.RequestServices.GetServices<IAuthenticationClaimsEnricher>();
        var principal = context.Principal;
        var email = ResolveEmail(principal);
        var name = ResolveName(principal);

        if (email == null || principal == null)
        {
            context.Response.Redirect("/error");
            context.Fail("Login error - missing user email");
            context.HandleResponse();
            return;
        }

        if (principal.Identity == null)
        {
            context.Response.Redirect("/error");
            context.Fail("Login error - missing user identity");
            context.HandleResponse();
            return;
        }

        var oldIdentity = (ClaimsIdentity)principal.Identity;
        var newIdentity = new ClaimsIdentity(
            oldIdentity.Claims,
            oldIdentity.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role
        );

        // Replace the incoming identity so role claims are controlled by local user resolution.
        context.Principal = new ClaimsPrincipal(newIdentity);

        var authUser = await settings.ResolveUser(email, name);
        if (authUser == null)
        {
            context.Response.Redirect("/access-denied");
            context.Fail("Not allowed");
            context.HandleResponse();
            return;
        }

        foreach (var enricher in claimEnrichers)
        {
            await enricher.Enrich(newIdentity, authUser, context);
        }
    }

    static string? ResolveEmail(ClaimsPrincipal? principal)
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

    static string? ResolveName(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return null;
        }

        return
            principal.Identity?.Name
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? principal.FindFirst("given_name")?.Value;
    }
}
