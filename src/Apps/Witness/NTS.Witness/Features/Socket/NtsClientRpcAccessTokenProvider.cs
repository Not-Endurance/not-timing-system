using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.Provider;
using Not.Application.RPC.SignalR;
using Not.Injection;
using Not.Notify;

namespace NTS.Witness.Features.Socket;

public class NtsClientRpcAccessTokenProvider : IRpcAccessTokenProvider, IScoped
{
    readonly IAccessTokenProvider _accessTokenProvider;
    readonly NClientAuthenticationSettings _clientAuthenticationSettings;
    readonly NavigationManager _navigationManager;
    readonly IWitnessAuthenticationRedirector _authenticationRedirector;
    readonly INotifier _notifier;

    public NtsClientRpcAccessTokenProvider(
        IAccessTokenProvider accessTokenProvider,
        IOptions<NClientAuthenticationSettings> clientAuthenticationOptions,
        NavigationManager navigationManager,
        IWitnessAuthenticationRedirector authenticationRedirector,
        INotifier notifier
    )
    {
        _accessTokenProvider = accessTokenProvider;
        _clientAuthenticationSettings = clientAuthenticationOptions.Value;
        _navigationManager = navigationManager;
        _authenticationRedirector = authenticationRedirector;
        _notifier = notifier;
    }

    public async Task<string?> Get()
    {
        var scope = ResolveScope();
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var requestOptions = new AccessTokenRequestOptions { Scopes = [scope], ReturnUrl = _navigationManager.Uri };
        try
        {
            var tokenResult = await _accessTokenProvider.RequestAccessToken(requestOptions);
            if (tokenResult.TryGetToken(out var token))
            {
                return token.Value;
            }

            if (tokenResult.Status == AccessTokenResultStatus.RequiresRedirect)
            {
                RedirectToAuthentication(scope, requestOptions.ReturnUrl);
            }

            CancelWithWarning("Witness could not acquire the Microsoft access token needed to connect.");
            return null;
        }
        catch (AccessTokenNotAvailableException)
        {
            RedirectToAuthentication(scope, requestOptions.ReturnUrl);
            return null;
        }
        catch (Exception ex) when (IsRefreshRequired(ex))
        {
            RedirectToAuthentication(scope, requestOptions.ReturnUrl, ex);
            return null;
        }
        catch (Exception ex)
        {
            CancelWithWarning(
                "Witness could not acquire the Microsoft access token needed to connect. Please sign in again.",
                ex
            );
            return null;
        }
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

    void RedirectToAuthentication(string scope, string returnUrl, Exception? exception = null)
    {
        _authenticationRedirector.RedirectToSignIn(scope, returnUrl);
        throw new OperationCanceledException("Witness authentication redirect started.", exception);
    }

    void CancelWithWarning(string message, Exception? exception = null)
    {
        _notifier.Warn(message);
        throw new OperationCanceledException(message, exception);
    }

    static bool IsRefreshRequired(Exception exception)
    {
        var message = exception.Message;
        if (
            message.Contains("refresh", StringComparison.OrdinalIgnoreCase)
            && (
                message.Contains("required", StringComparison.OrdinalIgnoreCase)
                || message.Contains("token", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return true;
        }

        return exception.InnerException != null && IsRefreshRequired(exception.InnerException);
    }
}
