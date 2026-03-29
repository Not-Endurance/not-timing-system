using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.Provider;
using Not.Application.RPC.SignalR;
using Not.Injection;

namespace NTS.Witness.Features.Socket;

public class NtsClientRpcAccessTokenProvider : IRpcAccessTokenProvider, IScoped
{
    readonly IAccessTokenProvider _accessTokenProvider;
    readonly NClientAuthenticationSettings _clientAuthenticationSettings;

    public NtsClientRpcAccessTokenProvider(
        IAccessTokenProvider accessTokenProvider,
        IOptions<NClientAuthenticationSettings> clientAuthenticationOptions
    )
    {
        _accessTokenProvider = accessTokenProvider;
        _clientAuthenticationSettings = clientAuthenticationOptions.Value;
    }

    public async Task<string?> Get()
    {
        var scope = ResolveScope();
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var tokenResult = await _accessTokenProvider.RequestAccessToken(
            new AccessTokenRequestOptions { Scopes = [scope] }
        );

        return tokenResult.TryGetToken(out var token) ? token.Value : null;
    }

    string? ResolveScope()
    {
        var scope = _clientAuthenticationSettings.Scope?.Trim();
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        if (scope.Contains("://", StringComparison.Ordinal))
        {
            return scope;
        }

        var audience = ResolveAudience();
        return string.IsNullOrWhiteSpace(audience) ? null : $"{audience}/{scope.TrimStart('/')}";
    }

    string? ResolveAudience()
    {
        var explicitAudience = _clientAuthenticationSettings.Audience?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitAudience))
        {
            return explicitAudience;
        }

        var clientId = _clientAuthenticationSettings.ResourceClientId?.Trim();
        return string.IsNullOrWhiteSpace(clientId) ? null : $"api://{clientId}";
    }
}
