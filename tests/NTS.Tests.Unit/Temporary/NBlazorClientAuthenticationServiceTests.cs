using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Client.Authentication.Services;

namespace NTS.Tests.Unit.Temporary;

public sealed class NBlazorClientAuthenticationServiceTests
{
    const string BASE_URI = "https://witness.test/";

    [Fact]
    public async Task Signout_clears_the_local_session()
    {
        var session = new RecordingAuthenticationSession();
        var navigation = new RecordingNavigationManager(BASE_URI, $"{BASE_URI}startlist");

        await CreateService(session, navigation).Signout();

        Assert.True(session.WasCleared);
    }

    [Fact]
    public async Task Signout_goes_through_the_real_logout_instead_of_the_authentication_page()
    {
        var navigation = new RecordingNavigationManager(BASE_URI, $"{BASE_URI}startlist");

        await CreateService(new RecordingAuthenticationSession(), navigation).Signout();

        Assert.Equal(RemoteAuthenticationDefaults.LogoutPath, navigation.NavigatedTo);
        Assert.Equal(nameof(InteractionType.SignOut), ReadLogoutRequest(navigation).Interaction);
    }

    [Fact]
    public async Task Signout_returns_the_visitor_to_the_page_they_signed_out_from()
    {
        var navigation = new RecordingNavigationManager(BASE_URI, $"{BASE_URI}startlist");

        await CreateService(new RecordingAuthenticationSession(), navigation).Signout();

        Assert.Equal($"{BASE_URI}startlist", ReadLogoutRequest(navigation).ReturnUrl);
    }

    [Fact]
    public async Task Signout_from_an_authentication_route_returns_to_the_app_root()
    {
        var navigation = new RecordingNavigationManager(
            BASE_URI,
            $"{BASE_URI}{AuthenticationContents.AUTHENTICATION}"
        );

        await CreateService(new RecordingAuthenticationSession(), navigation).Signout();

        Assert.Equal(BASE_URI, ReadLogoutRequest(navigation).ReturnUrl);
    }

    static NBlazorClientAuthenticationService CreateService(
        INAuthenticationSession session,
        NavigationManager navigation
    )
    {
        return new NBlazorClientAuthenticationService(new StubAuthenticationSessionStorage(), session, navigation);
    }

    /// <summary>
    /// The logout route only acts on a navigation that carries this state; a plain navigation is
    /// rejected as not initiated from within the page. Blazor's own serializer context for it is
    /// internal, so the state is read back through a matching shape.
    /// </summary>
    static LogoutRequest ReadLogoutRequest(RecordingNavigationManager navigation)
    {
        var state = navigation.NavigationOptions?.HistoryEntryState;
        Assert.NotNull(state);

        var request = JsonSerializer.Deserialize<LogoutRequest>(
            state,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(request);
        return request;
    }

    sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager(string baseUri, string uri)
        {
            Initialize(baseUri, uri);
        }

        public string? NavigatedTo { get; private set; }
        public NavigationOptions? NavigationOptions { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            NavigatedTo = uri;
            NavigationOptions = options;
        }
    }

    sealed class RecordingAuthenticationSession : INAuthenticationSession
    {
        public bool WasCleared { get; private set; }

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
            WasCleared = true;
            return Task.CompletedTask;
        }
    }

    sealed class StubAuthenticationSessionStorage : INAuthenticationSessionStorage
    {
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
            return Task.CompletedTask;
        }

        public Task WriteSigninFlowStartedAt()
        {
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> ReadSigninFlowStartedAtAsync()
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }

        public Task ClearSigninFlowStartedAt()
        {
            return Task.CompletedTask;
        }
    }

    sealed record LogoutRequest
    {
        public string? ReturnUrl { get; init; }
        public string? Interaction { get; init; }
    }
}
