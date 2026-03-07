using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Authentication.WebAssembly.Msal.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Not.Application.Authentication.User;

internal class NWasmAccountClaimsPrincipalFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
{
    readonly NUserResolver _userResolver;
    readonly NavigationManager _navigator;
    readonly IOptionsSnapshot<RemoteAuthenticationOptions<MsalProviderOptions>> _authOptions;
    readonly ILogger<NWasmAccountClaimsPrincipalFactory> _logger;

    public NWasmAccountClaimsPrincipalFactory(
        IAccessTokenProviderAccessor accessor,
        NUserResolver userResolver,
        NavigationManager navigator,
        IOptionsSnapshot<RemoteAuthenticationOptions<MsalProviderOptions>> authOptions,
        ILogger<NWasmAccountClaimsPrincipalFactory> logger
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

        var result = await _userResolver.ResolvePrincipal(principal);
        if (result.IsSuccess)
        {
            return result.Principal;
        }

        _logger.LogWarning("Client authentication failed during local user resolution. Reason: {reason}", result.Error);

        var failedPath =
            _authOptions.Value.AuthenticationPaths.LogInFailedPath
            ?? _authOptions.Value.AuthenticationPaths.LogOutFailedPath
            ?? "/unauthorized";

        _navigator.NavigateTo(failedPath, forceLoad: false);
        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}
