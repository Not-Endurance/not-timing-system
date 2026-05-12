using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Client.Authentication.Components;
using Not.Blazor.Client.Authentication.Services;

namespace NTS.Authentication.Tests;

public class NBlazorClientAuthenticationServiceTests
{
    [Fact]
    public async Task Signin_writes_flow_marker_before_redirecting_to_msal()
    {
        var events = new List<string>();
        var markers = new RecordingAuthenticationSessionStorage(events);
        var navigation = new RecordingNavigationManager(events);
        var service = new NBlazorClientAuthenticationService(
            markers,
            new RecordingAuthenticationSession(events),
            navigation
        );

        await service.Signin();

        Assert.Equal(["flow-write", "navigate"], events);
        Assert.Equal(RemoteAuthenticationDefaults.LoginPath, navigation.LastRelativeUri);
    }

    [Fact]
    public async Task Signout_clears_local_auth_state_before_returning_to_authentication_page()
    {
        var events = new List<string>();
        var markers = new RecordingAuthenticationSessionStorage(events);
        var navigation = new RecordingNavigationManager(events);
        var service = new NBlazorClientAuthenticationService(
            markers,
            new RecordingAuthenticationSession(events),
            navigation
        );

        await service.Signout();

        Assert.Equal(["session-clear", "navigate"], events);
        Assert.Equal("authentication", navigation.LastRelativeUri);
    }

    [Fact]
    public async Task Register_redirect_action_returns_to_unified_authentication_page()
    {
        var events = new List<string>();
        var navigation = new RecordingNavigationManager(events);
        var component = new RecordingAuthenticateRedirectContent();
        component.SetAction(RemoteAuthenticationActions.Register);
        InjectNavigationManager(component, navigation);

        component.RunParametersSet();

        Assert.False(component.RenderRemoteAuthenticator);

        await component.RunAfterRenderAsync(firstRender: true);

        Assert.Equal(["navigate"], events);
        Assert.Equal("authentication", navigation.LastRelativeUri);
    }

    static void InjectNavigationManager(AuthenticateRedirectContentBehind component, NavigationManager navigation)
    {
        var property = typeof(AuthenticateRedirectContentBehind).GetProperty(
            "NavigationManager",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.NotNull(property);
        property!.SetValue(component, navigation);
    }

    sealed class RecordingAuthenticationSessionStorage : INAuthenticationSessionStorage
    {
        readonly List<string> _events;

        public RecordingAuthenticationSessionStorage(List<string> events)
        {
            _events = events;
        }

        public Task<DateTimeOffset?> ReadSessionStartedAtAsync()
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }

        public Task WriteSessionStartedAt(DateTimeOffset startedAtUtc)
        {
            return Task.CompletedTask;
        }

        public Task ClearSessionStartedAt()
        {
            _events.Add("session-start-clear");
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> ReadSigninFlowStartedAtAsync()
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }

        public Task WriteSigninFlowStartedAt()
        {
            _events.Add("flow-write");
            return Task.CompletedTask;
        }

        public Task ClearSigninFlowStartedAt()
        {
            _events.Add("flow-clear");
            return Task.CompletedTask;
        }
    }

    sealed class RecordingAuthenticationSession : INAuthenticationSession
    {
        readonly List<string> _events;

        public RecordingAuthenticationSession(List<string> events)
        {
            _events = events;
        }

        public Task<bool> ShouldTryAutoSignin()
        {
            return Task.FromResult(false);
        }

        public Task<bool> HasActiveSession()
        {
            return Task.FromResult(false);
        }

        public Task Commit()
        {
            return Task.CompletedTask;
        }

        public Task Clear()
        {
            _events.Add("session-clear");
            return Task.CompletedTask;
        }
    }

    sealed class RecordingNavigationManager : NavigationManager
    {
        readonly List<string> _events;

        public RecordingNavigationManager(List<string> events)
        {
            _events = events;
            Initialize("https://nts.test/", "https://nts.test/authentication");
        }

        public string? LastRelativeUri { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            LastRelativeUri = ToBaseRelativePath(ToAbsoluteUri(uri).AbsoluteUri);
            _events.Add("navigate");
        }
    }

    sealed class RecordingAuthenticateRedirectContent : AuthenticateRedirectContentBehind
    {
        public bool RenderRemoteAuthenticator => ShouldRenderRemoteAuthenticator;

        public void SetAction(string action)
        {
            Action = action;
        }

        public void RunParametersSet()
        {
            OnParametersSet();
        }

        public Task RunAfterRenderAsync(bool firstRender)
        {
            return OnAfterRenderAsync(firstRender);
        }
    }
}
