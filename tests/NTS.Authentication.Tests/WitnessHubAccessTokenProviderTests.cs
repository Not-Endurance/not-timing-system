using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.Provider;
using Not.Notify;
using NTS.Witness.Features.Socket;

namespace NTS.Authentication.Tests;

public class WitnessHubAccessTokenProviderTests
{
    [Fact]
    public async Task Get_returns_null_when_scope_is_not_configured()
    {
        var accessTokenProvider = new RecordingAccessTokenProvider();
        var context = CreateContext(
            accessTokenProvider,
            new NClientAuthenticationSettings { ResourceClientId = "resource-client" },
            new RecordingAuthenticationRedirector()
        );

        var token = await context.Provider.Get();

        Assert.Null(token);
        Assert.Equal(0, accessTokenProvider.RequestCalls);
    }

    [Fact]
    public async Task Get_returns_access_token_when_request_succeeds()
    {
        var accessTokenProvider = new RecordingAccessTokenProvider
        {
            ResultFactory = _ => new ValueTask<AccessTokenResult>(
                new AccessTokenResult(
                    AccessTokenResultStatus.Success,
                    new AccessToken
                    {
                        Value = "access-token",
                        Expires = DateTimeOffset.UtcNow.AddMinutes(5),
                        GrantedScopes = ["api://resource-client/nts-client-scope"],
                    },
                    redirectUrl: ""
                )
            ),
        };
        var context = CreateContext(
            accessTokenProvider,
            new NClientAuthenticationSettings { ResourceClientId = "resource-client", Scope = "nts-client-scope" },
            new RecordingAuthenticationRedirector()
        );

        var token = await context.Provider.Get();

        Assert.Equal("access-token", token);
        Assert.Equal(["api://resource-client/nts-client-scope"], accessTokenProvider.LastOptions?.Scopes?.ToArray());
        Assert.Equal("https://localhost/performance", accessTokenProvider.LastOptions?.ReturnUrl);
    }

    [Fact]
    public async Task Get_redirects_when_access_token_requires_interaction()
    {
        var accessTokenProvider = new RecordingAccessTokenProvider
        {
            ResultFactory = _ => new ValueTask<AccessTokenResult>(
                new AccessTokenResult(
                    AccessTokenResultStatus.RequiresRedirect,
                    new AccessToken(),
                    interactiveRequestUrl: "authentication/login",
                    interactiveRequest: new InteractiveRequestOptions
                    {
                        Interaction = InteractionType.GetToken,
                        ReturnUrl = "https://localhost/performance",
                        Scopes = ["api://resource-client/nts-client-scope"],
                    }
                )
            ),
        };
        var redirector = new RecordingAuthenticationRedirector();
        var context = CreateContext(
            accessTokenProvider,
            new NClientAuthenticationSettings { ResourceClientId = "resource-client", Scope = "nts-client-scope" },
            redirector
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Provider.Get());

        Assert.Equal(1, redirector.AccessTokenResultRedirectCalls);
        Assert.Equal("authentication/login", redirector.LastAccessTokenResult?.InteractiveRequestUrl);
        Assert.Equal("https://localhost/performance", redirector.LastAccessTokenResult?.InteractionOptions?.ReturnUrl);
    }

    [Fact]
    public async Task Get_redirects_when_token_refresh_required_error_is_thrown()
    {
        var accessTokenProvider = new RecordingAccessTokenProvider
        {
            ExceptionToThrow = new InvalidOperationException(
                "ClientAuthError: token_refresh_required: Cannot return token from cache because it must be refreshed."
            ),
        };
        var redirector = new RecordingAuthenticationRedirector();
        var context = CreateContext(
            accessTokenProvider,
            new NClientAuthenticationSettings { ResourceClientId = "resource-client", Scope = "nts-client-scope" },
            redirector
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Provider.Get());

        Assert.Equal(1, redirector.InteractiveRequestRedirectCalls);
        Assert.Equal(InteractionType.GetToken, redirector.LastInteractiveRequest?.Interaction);
        Assert.Equal("https://localhost/performance", redirector.LastInteractiveRequest?.ReturnUrl);
        Assert.Equal(["api://resource-client/nts-client-scope"], redirector.LastInteractiveRequest?.Scopes?.ToArray());
    }

    [Fact]
    public async Task Get_redirects_when_blazor_reports_access_token_not_available()
    {
        var exception = new AccessTokenNotAvailableException(
            new TestNavigationManager(),
            new AccessTokenResult(
                AccessTokenResultStatus.RequiresRedirect,
                new AccessToken(),
                interactiveRequestUrl: "authentication/login",
                interactiveRequest: new InteractiveRequestOptions
                {
                    Interaction = InteractionType.GetToken,
                    ReturnUrl = "https://localhost/performance",
                    Scopes = ["api://resource-client/nts-client-scope"],
                }
            ),
            ["api://resource-client/nts-client-scope"]
        );
        var accessTokenProvider = new RecordingAccessTokenProvider { ExceptionToThrow = exception };
        var redirector = new RecordingAuthenticationRedirector();
        var context = CreateContext(
            accessTokenProvider,
            new NClientAuthenticationSettings { ResourceClientId = "resource-client", Scope = "nts-client-scope" },
            redirector
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Provider.Get());

        Assert.Equal(1, redirector.AccessTokenExceptionRedirectCalls);
        Assert.Same(exception, redirector.LastAccessTokenException);
    }

    [Fact]
    public async Task Get_warns_and_cancels_when_non_auth_exception_is_thrown()
    {
        var accessTokenProvider = new RecordingAccessTokenProvider
        {
            ExceptionToThrow = new InvalidOperationException("Something else failed."),
        };
        var redirector = new RecordingAuthenticationRedirector();
        var context = CreateContext(
            accessTokenProvider,
            new NClientAuthenticationSettings { ResourceClientId = "resource-client", Scope = "nts-client-scope" },
            redirector
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Provider.Get());

        Assert.Equal(0, redirector.TotalCalls);
        Assert.Equal(
            ["Witness could not acquire the Microsoft access token needed to connect. Please sign in again."],
            context.Notifier.Warnings
        );
    }

    static TestContext CreateContext(
        IAccessTokenProvider accessTokenProvider,
        NClientAuthenticationSettings settings,
        RecordingAuthenticationRedirector redirector
    )
    {
        var notifier = new TestNotifier();

        return new TestContext(
            new NtsClientRpcAccessTokenProvider(
                accessTokenProvider,
                Options.Create(settings),
                new TestNavigationManager(),
                redirector,
                notifier
            ),
            notifier
        );
    }

    sealed class TestContext
    {
        public TestContext(NtsClientRpcAccessTokenProvider provider, TestNotifier notifier)
        {
            Provider = provider;
            Notifier = notifier;
        }

        public NtsClientRpcAccessTokenProvider Provider { get; }

        public TestNotifier Notifier { get; }
    }

    sealed class RecordingAccessTokenProvider : IAccessTokenProvider
    {
        public Func<AccessTokenRequestOptions?, ValueTask<AccessTokenResult>>? ResultFactory { get; init; }
        public Exception? ExceptionToThrow { get; init; }
        public int RequestCalls { get; private set; }
        public AccessTokenRequestOptions? LastOptions { get; private set; }

        public ValueTask<AccessTokenResult> RequestAccessToken()
        {
            return RequestAccessToken(null!);
        }

        public ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
        {
            RequestCalls++;
            LastOptions = options;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return ResultFactory?.Invoke(options)
                ?? new ValueTask<AccessTokenResult>(
                    new AccessTokenResult(
                        AccessTokenResultStatus.RequiresRedirect,
                        new AccessToken(),
                        interactiveRequestUrl: "authentication/login",
                        interactiveRequest: new InteractiveRequestOptions
                        {
                            Interaction = InteractionType.GetToken,
                            ReturnUrl = "https://localhost/performance",
                            Scopes = ["api://resource-client/nts-client-scope"],
                        }
                    )
                );
        }
    }

    sealed class RecordingAuthenticationRedirector : IWitnessAuthenticationRedirector
    {
        public int AccessTokenResultRedirectCalls { get; private set; }
        public int AccessTokenExceptionRedirectCalls { get; private set; }
        public int InteractiveRequestRedirectCalls { get; private set; }
        public int TotalCalls =>
            AccessTokenResultRedirectCalls + AccessTokenExceptionRedirectCalls + InteractiveRequestRedirectCalls;
        public AccessTokenResult? LastAccessTokenResult { get; private set; }
        public AccessTokenNotAvailableException? LastAccessTokenException { get; private set; }
        public InteractiveRequestOptions? LastInteractiveRequest { get; private set; }

        public void RedirectToSignIn(AccessTokenResult tokenResult)
        {
            AccessTokenResultRedirectCalls++;
            LastAccessTokenResult = tokenResult;
        }

        public void RedirectToSignIn(AccessTokenNotAvailableException exception)
        {
            AccessTokenExceptionRedirectCalls++;
            LastAccessTokenException = exception;
        }

        public void RedirectToSignIn(InteractiveRequestOptions requestOptions)
        {
            InteractiveRequestRedirectCalls++;
            LastInteractiveRequest = requestOptions;
        }
    }

    sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/performance");
        }

        protected override void NavigateToCore(string uri, bool forceLoad) { }

        protected override void NavigateToCore(string uri, NavigationOptions options) { }
    }

    sealed class TestNotifier : INotifier
    {
        public List<string> Warnings { get; } = [];

        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message)
        {
            Warnings.Add(message);
        }

        public void Warn(IEnumerable<string> messages)
        {
            Warnings.AddRange(messages);
        }

        public void Error(string message) { }

        public void Error(Exception ex) { }
    }
}
