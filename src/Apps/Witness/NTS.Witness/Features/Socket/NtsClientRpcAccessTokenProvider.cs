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
        var scope = NClientAuthenticationSettingsScopeResolver.ResolveScope(_clientAuthenticationSettings);
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
                RedirectToAuthentication(tokenResult);
            }

            CancelWithWarning("Witness could not acquire the Microsoft access token needed to connect.");
            return null;
        }
        catch (AccessTokenNotAvailableException ex)
        {
            RedirectToAuthentication(ex);
            return null;
        }
        catch (Exception ex) when (IsInteractiveAuthenticationRequired(ex))
        {
            RedirectToAuthentication(CreateInteractiveRequestOptions(scope, requestOptions.ReturnUrl), ex);
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

    static InteractiveRequestOptions CreateInteractiveRequestOptions(string scope, string returnUrl)
    {
        return new InteractiveRequestOptions
        {
            Interaction = InteractionType.GetToken,
            ReturnUrl = returnUrl,
            Scopes = [scope],
        };
    }

    void RedirectToAuthentication(AccessTokenResult tokenResult, Exception? exception = null)
    {
        _authenticationRedirector.RedirectToSignIn(tokenResult);
        throw new OperationCanceledException("Witness authentication redirect started.", exception);
    }

    void RedirectToAuthentication(AccessTokenNotAvailableException exception)
    {
        _authenticationRedirector.RedirectToSignIn(exception);
        throw new OperationCanceledException("Witness authentication redirect started.", exception);
    }

    void RedirectToAuthentication(InteractiveRequestOptions requestOptions, Exception? exception = null)
    {
        _authenticationRedirector.RedirectToSignIn(requestOptions);
        throw new OperationCanceledException("Witness authentication redirect started.", exception);
    }

    void CancelWithWarning(string message, Exception? exception = null)
    {
        _notifier.Warn(message);
        throw new OperationCanceledException(message, exception);
    }

    static bool IsInteractiveAuthenticationRequired(Exception exception)
    {
        var message = exception.Message;
        if (message.Contains("token_refresh_required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (message.Contains("interaction_required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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

        return exception.InnerException != null && IsInteractiveAuthenticationRequired(exception.InnerException);
    }
}
