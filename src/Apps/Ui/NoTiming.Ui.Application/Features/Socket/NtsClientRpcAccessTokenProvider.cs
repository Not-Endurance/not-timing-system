using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.Provider;
using Not.Application.RPC.SignalR;
using Not.Blazor.Client.Authentication;
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
    readonly ILogger<NtsClientRpcAccessTokenProvider> _logger;

    public NtsClientRpcAccessTokenProvider(
        IAccessTokenProvider accessTokenProvider,
        IOptions<NClientAuthenticationSettings> clientAuthenticationOptions,
        NavigationManager navigationManager,
        IWitnessAuthenticationRedirector authenticationRedirector,
        INotifier notifier,
        ILogger<NtsClientRpcAccessTokenProvider> logger
    )
    {
        _accessTokenProvider = accessTokenProvider;
        _clientAuthenticationSettings = clientAuthenticationOptions.Value;
        _navigationManager = navigationManager;
        _authenticationRedirector = authenticationRedirector;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<string?> Get()
    {
        var scope = NClientAuthenticationSettingsScopeResolver.ResolveScope(_clientAuthenticationSettings);
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var requestOptions = new AccessTokenRequestOptions
        {
            Scopes = [scope],
            ReturnUrl = GetSafeReturnUrl(_navigationManager.Uri),
        };
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
            ReturnUrl = GetSafeReturnUrl(returnUrl),
            Scopes = [scope],
        };
    }

    void RedirectToAuthentication(AccessTokenResult tokenResult, Exception? exception = null)
    {
        if (IsOnAuthenticationRoute())
        {
            _logger.LogWarning(
                exception,
                "Skipping witness auth redirect because the app is already on an authentication route. Status: {Status}",
                tokenResult.Status
            );
            throw new OperationCanceledException(
                "Witness authentication redirect suppressed on auth route.",
                exception
            );
        }

        _authenticationRedirector.RedirectToSignIn(tokenResult);
        throw new OperationCanceledException("Witness authentication redirect started.", exception);
    }

    void RedirectToAuthentication(AccessTokenNotAvailableException exception)
    {
        if (IsOnAuthenticationRoute())
        {
            _logger.LogWarning(
                exception,
                "Skipping witness auth redirect because the app is already on an authentication route."
            );
            throw new OperationCanceledException(
                "Witness authentication redirect suppressed on auth route.",
                exception
            );
        }

        _authenticationRedirector.RedirectToSignIn(exception);
        throw new OperationCanceledException("Witness authentication redirect started.", exception);
    }

    void RedirectToAuthentication(InteractiveRequestOptions requestOptions, Exception? exception = null)
    {
        if (IsOnAuthenticationRoute())
        {
            _logger.LogWarning(
                exception,
                "Skipping witness auth redirect because the app is already on an authentication route."
            );
            throw new OperationCanceledException(
                "Witness authentication redirect suppressed on auth route.",
                exception
            );
        }

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

    bool IsOnAuthenticationRoute()
    {
        return IsAuthenticationRoute(_navigationManager.Uri);
    }

    static string GetSafeReturnUrl(string currentUri)
    {
        return IsAuthenticationRoute(currentUri) ? "/" : currentUri;
    }

    static bool IsAuthenticationRoute(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri))
        {
            return false;
        }

        var path = absoluteUri.AbsolutePath.Trim('/');
        return path.StartsWith(AuthenticationContents.AUTHENTICATION, StringComparison.OrdinalIgnoreCase);
    }
}
