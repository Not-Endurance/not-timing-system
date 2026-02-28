using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Not.Application.Authentication.Abstractions;

namespace Not.Application.Authentication.User;

internal class NUserResolver : IUserResolver
{
    const string ERROR_PAGE = "/error";
    const string ACCESS_DENIED_PAGE = "/access-denied";

    readonly IUserRegister _userRegister;
    readonly ILogger<NUserResolver> _logger;

    public NUserResolver(IUserRegister userRegister, ILogger<NUserResolver> logger)
    {
        _userRegister = userRegister;
        _logger = logger;
    }

    internal async Task<NUserResolutionResult> ResolvePrincipal(ClaimsPrincipal? principal)
    {
        var email = ResolveEmail(principal);
        if (email == null || principal == null)
        {
            return NUserResolutionResult.Failure("Login error - missing user email", ERROR_PAGE);
        }

        if (principal.Identity is not ClaimsIdentity oldIdentity)
        {
            return NUserResolutionResult.Failure("Login error - missing user identity", ERROR_PAGE);
        }

        var newIdentity = new ClaimsIdentity(
            oldIdentity.Claims,
            oldIdentity.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role
        );

        // Replace the incoming identity so role claims are controlled by local user resolution.
        var resolvedPrincipal = new ClaimsPrincipal(newIdentity);

        var userResult = await _userRegister.Get(email);
        if (userResult.IsError || userResult.Data == null)
        {
            userResult = await _userRegister.Register(email);
            if (userResult.IsError)
            {
                _logger.LogError("Authentication failed: {errors}", string.Join(",", userResult.Errors));
                return NUserResolutionResult.Failure("Not allowed", ACCESS_DENIED_PAGE);
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

        return NUserResolutionResult.Success(resolvedPrincipal);
    }

    public async Task Resolve(TicketReceivedContext context)
    {
        var result = await ResolvePrincipal(context.Principal);
        if (!result.IsSuccess)
        {
            context.Response.Redirect(result.FailurePath);
            context.Fail(result.Error);
            context.HandleResponse();
            return;
        }

        context.Principal = result.Principal;
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

internal readonly record struct NUserResolutionResult(
    ClaimsPrincipal Principal,
    string Error,
    string FailurePath,
    bool IsSuccess
)
{
    public static NUserResolutionResult Success(ClaimsPrincipal principal)
    {
        return new NUserResolutionResult(principal, string.Empty, string.Empty, IsSuccess: true);
    }

    public static NUserResolutionResult Failure(string error, string failurePath)
    {
        return new NUserResolutionResult(
            new ClaimsPrincipal(new ClaimsIdentity()),
            error,
            failurePath,
            IsSuccess: false
        );
    }
}

public interface IUserResolver
{
    public Task Resolve(TicketReceivedContext context);
}
