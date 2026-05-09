using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Not.Application.Authentication.Abstractions;

namespace Not.Application.Authentication.User;

public class NUserResolver
{
    const string ERROR_PAGE = "/error";
    const string ACCESS_DENIED_PAGE = "/authentication";

    readonly IUserRegister _userRegister;
    readonly ILogger<NUserResolver> _logger;

    public NUserResolver(IUserRegister userRegister, ILogger<NUserResolver> logger)
    {
        _userRegister = userRegister;
        _logger = logger;
    }

    public async Task<NUserResolutionResult> ResolvePrincipal(ClaimsPrincipal? principal)
    {
        return await ResolvePrincipal(principal, null);
    }

    public async Task<NUserResolutionResult> ResolvePrincipal(
        ClaimsPrincipal? principal,
        NUserRegistrationProfile? profile
    )
    {
        var registration = NUserClaimsHelper.ResolveRegistration(principal, profile);
        if (registration == null || principal == null)
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

        var userResult = await _userRegister.Get(registration.Email);
        if (!userResult.IsSuccess || userResult.Data == null)
        {
            userResult = await _userRegister.Register(registration);
            if (!userResult.IsSuccess || userResult.Data == null)
            {
                var errors = string.Join(",", userResult.Errors);
                _logger.LogError("Authentication failed: {errors}", errors);
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
}

public readonly record struct NUserResolutionResult
{
    public static NUserResolutionResult Success(ClaimsPrincipal principal)
    {
        return new NUserResolutionResult(true, principal: principal);
    }

    public static NUserResolutionResult Failure(string error, string serverRedirect)
    {
        return new NUserResolutionResult(false, error: error, serverRedirect: serverRedirect);
    }

    public NUserResolutionResult(
        bool isSuccess,
        ClaimsPrincipal? principal = null,
        string? error = null,
        string? serverRedirect = null
    )
    {
        IsSuccess = isSuccess;
        Principal = principal;
        Error = error;
        ServerRedirect = serverRedirect;
    }

    [MemberNotNullWhen(true, nameof(Principal))]
    [MemberNotNullWhen(false, nameof(Error), nameof(ServerRedirect))]
    public bool IsSuccess { get; }
    public ClaimsPrincipal? Principal { get; }
    public string? Error { get; }
    public string? ServerRedirect { get; }
}
