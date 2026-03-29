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
        var service = CreateService(
            accessTokenProvider,
            new NClientAuthenticationSettings { ResourceClientId = "resource-client" },
            new RecordingAuthenticationRedirector()
        );

        var token = await service.Get();

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
        var service = CreateService(
            accessTokenProvider,
            new NClientAuthenticationSettings
            {
                ResourceClientId = "resource-client",
                Scope = "nts-client-scope",
            },
            new RecordingAuthenticationRedirector()
        );

        var token = await service.Get();

        Assert.Equal("access-token", token);
        Assert.Equal(
            ["api://resource-client/nts-client-scope"],
            accessTokenProvider.LastOptions?.Scopes?.ToArray()
        );
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
        var service = CreateService(
            accessTokenProvider,
            new NClientAuthenticationSettings
            {
                ResourceClientId = "resource-client",
                Scope = "nts-client-scope",
            },
            redirector
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.Get());

        Assert.Equal(1, redirector.Calls);
        Assert.Equal("api://resource-client/nts-client-scope", redirector.LastScope);
        Assert.Equal("https://localhost/performance", redirector.LastReturnUrl);
    }

    [Fact]
    public async Task Get_redirects_when_refresh_required_error_is_thrown()
    {
        var accessTokenProvider = new RecordingAccessTokenProvider
        {
            ExceptionToThrow = new InvalidOperationException("A refresh is required to get the token."),
        };
        var redirector = new RecordingAuthenticationRedirector();
        var service = CreateService(
            accessTokenProvider,
            new NClientAuthenticationSettings
            {
                ResourceClientId = "resource-client",
                Scope = "nts-client-scope",
            },
            redirector
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.Get());

        Assert.Equal(1, redirector.Calls);
        Assert.Equal("api://resource-client/nts-client-scope", redirector.LastScope);
        Assert.Equal("https://localhost/performance", redirector.LastReturnUrl);
    }

    [Fact]
    public async Task Get_redirects_when_blazor_reports_access_token_not_available()
    {
        var accessTokenProvider = new RecordingAccessTokenProvider
        {
            ExceptionToThrow = new AccessTokenNotAvailableException(
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
            ),
        };
        var redirector = new RecordingAuthenticationRedirector();
        var service = CreateService(
            accessTokenProvider,
            new NClientAuthenticationSettings
            {
                ResourceClientId = "resource-client",
                Scope = "nts-client-scope",
            },
            redirector
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.Get());

        Assert.Equal(1, redirector.Calls);
        Assert.Equal("api://resource-client/nts-client-scope", redirector.LastScope);
    }

    static NtsClientRpcAccessTokenProvider CreateService(
        IAccessTokenProvider accessTokenProvider,
        NClientAuthenticationSettings settings,
        RecordingAuthenticationRedirector redirector
    )
    {
        return new NtsClientRpcAccessTokenProvider(
            accessTokenProvider,
            Options.Create(settings),
            new TestNavigationManager(),
            redirector,
            new TestNotifier()
        );
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
        public int Calls { get; private set; }
        public string? LastScope { get; private set; }
        public string? LastReturnUrl { get; private set; }

        public void RedirectToSignIn(string scope, string returnUrl)
        {
            Calls++;
            LastScope = scope;
            LastReturnUrl = returnUrl;
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

        public void Error(string message) { }

        public void Error(Exception ex) { }
    }
}
