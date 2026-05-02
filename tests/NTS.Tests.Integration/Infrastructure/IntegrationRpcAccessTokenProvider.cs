using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.RPC.SignalR;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed class IntegrationRpcAccessTokenProvider : IRpcAccessTokenProvider
{
    readonly AuthenticationStateProvider _authentication;

    public IntegrationRpcAccessTokenProvider(AuthenticationStateProvider authentication)
    {
        _authentication = authentication;
    }

    public async Task<string?> Get()
    {
        var principal = (await _authentication.GetAuthenticationStateAsync()).User;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var name = principal.FindFirst("name")?.Value ?? email;
        return $"integration|{email}|{name}|nts-client-scope";
    }
}
