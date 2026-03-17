using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Extensions.Logging;
using Not.Application.Authentication.User;

namespace Not.Blazor.Client.Authentication.Services;

internal class ClientSideAccountClaimsPrincipalFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
{
    readonly ILocalStorageMarkerService _localStorageMarkerService;
    readonly NUserResolver _userResolver;
    readonly NavigationManager _navigator;
    readonly ILogger<ClientSideAccountClaimsPrincipalFactory> _logger;

    public ClientSideAccountClaimsPrincipalFactory(
        ILocalStorageMarkerService localStorageMarkerService,
        IAccessTokenProviderAccessor accessor,
        NUserResolver userResolver,
        NavigationManager navigator,
        ILogger<ClientSideAccountClaimsPrincipalFactory> logger
    )
        : base(accessor)
    {
        _localStorageMarkerService = localStorageMarkerService;
        _userResolver = userResolver;
        _navigator = navigator;
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

        if (await _localStorageMarkerService.IsSignedOut())
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var result = await _userResolver.ResolvePrincipal(principal);
        if (result.IsSuccess)
        {
            return result.Principal;
        }

        _logger.LogWarning("Client authentication failed during local user resolution. Reason: {reason}", result.Error);
        _navigator.NavigateTo(result.ServerRedirect ?? AuthenticationContents.AUTHENTICATION, forceLoad: false);
        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}
