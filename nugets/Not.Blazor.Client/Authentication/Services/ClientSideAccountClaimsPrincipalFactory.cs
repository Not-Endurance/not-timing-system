using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Authentication.WebAssembly.Msal.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.User;

namespace Not.Blazor.Client.Authentication.Services;

internal class ClientSideAccountClaimsPrincipalFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
{
    readonly NUserResolver _userResolver;
    readonly NavigationManager _navigator;
    readonly IOptionsSnapshot<RemoteAuthenticationOptions<MsalProviderOptions>> _authOptions;
    readonly ILogger<ClientSideAccountClaimsPrincipalFactory> _logger;

    public ClientSideAccountClaimsPrincipalFactory(
        IAccessTokenProviderAccessor accessor,
        NUserResolver userResolver,
        NavigationManager navigator,
        IOptionsSnapshot<RemoteAuthenticationOptions<MsalProviderOptions>> authOptions,
        ILogger<ClientSideAccountClaimsPrincipalFactory> logger
    )
        : base(accessor)
    {
        _userResolver = userResolver;
        _navigator = navigator;
        _authOptions = authOptions;
        _logger = logger;
    }

    public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount account,
        RemoteAuthenticationUserOptions options
    )
    {
        var principal = await base.CreateUserAsync(account, options);
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        if (IsLogoutFlow())
        {
            // During logout routes we should not attempt local user resolution,
            // otherwise an authenticated principal can be recreated before sign-out completes.
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var result = await _userResolver.ResolvePrincipal(principal);
        if (result.IsSuccess)
        {
            return result.Principal;
        }

        _logger.LogWarning("Client authentication failed during local user resolution. Reason: {reason}", result.Error);

        var isLogoutFlow = IsLogoutFlow();
        var paths = _authOptions.Value.AuthenticationPaths;
        var failedPath = isLogoutFlow
            ? (paths.LogOutFailedPath ?? paths.LogInFailedPath ?? NBlazorContents.AUTHENTICATE)
            : (paths.LogInFailedPath ?? paths.LogOutFailedPath ?? NBlazorContents.AUTHENTICATE);

        _navigator.NavigateTo(failedPath, forceLoad: false);
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    bool IsLogoutFlow()
    {
        var endpoint = _navigator.ToBaseRelativePath(_navigator.Uri);
        endpoint = NormalizeEndpoint(endpoint.Split('?')[0].Split('#')[0]);

        return endpoint is var normalized
            && (
                normalized == NormalizeEndpoint(NBlazorContents.SIGNOUT)
                || normalized == NormalizeEndpoint(NBlazorContents.SIGNOUT_CALLBACK)
                || normalized == NormalizeEndpoint(NBlazorContents.SIGNOUT_CALLBACK_ALT)
            );
    }

    static string NormalizeEndpoint(string endpoint)
    {
        return endpoint.Trim('/').ToLowerInvariant();
    }
}
