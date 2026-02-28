using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Not.Application.Authentication.Abstractions;

namespace Not.Application.Authentication.User;

internal class NUserResolver : IUserResolver
{
    readonly IUserRegister _userRegister;
    readonly ILogger<NUserResolver> _logger;

    public NUserResolver(IUserRegister userRegister, ILogger<NUserResolver> logger)
    {
        _userRegister = userRegister;
        _logger = logger;
    }

    public async Task Resolve(TicketReceivedContext context)
    {
        var principal = context.Principal;
        var email = ResolveEmail(principal);

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

        var userResult = await _userRegister.Get(email);
        if (userResult.IsError || userResult.Data == null)
        {
            userResult = await _userRegister.Register(email);
            if (userResult.IsError)
            {
                _logger.LogError("Authentication failed: {errors}", string.Join(",", userResult.Errors));
                context.Response.Redirect("/access-denied"); // TODO: parameterize this or use some sort of user feedback.
                context.Fail("Not allowed");
                context.HandleResponse();
                return;
            }
        }

        var user = userResult.Data;
        foreach (var role in user.Roles ?? [])
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                newIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
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
}

public interface IUserResolver
{
    public Task Resolve(TicketReceivedContext context);
}
