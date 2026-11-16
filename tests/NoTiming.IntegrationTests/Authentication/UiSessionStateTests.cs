using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NoTiming.IntegrationTests.Drivers;
using NoTiming.IntegrationTests.Infrastructure;

namespace NoTiming.IntegrationTests.Authentication;

/// <summary>
/// The Ui reads the signed-in state from the host's profile route. These tests serve the Ui under /ui/, as a deployed
/// Ui is served, and check that the state still comes from the API at the origin. A Ui that called the API under its own
/// path would receive its index.html, which is not the profile.
/// </summary>
public sealed class UiSessionStateTests : IClassFixture<NoTimingIntegrationFixture>
{
    readonly NoTimingIntegrationFixture _fixture;

    public UiSessionStateTests(NoTimingIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenVisitorIsAnonymous_ReturnsAnonymousState()
    {
        await using var ui = ServedUnderUiPath(sessionCookie: null, "UiSessionState-Anonymous");

        var state = await ui.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? true);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_WhenPersonIsSignedIn_ReturnsThePerson()
    {
        var user = new IntegrationUser("ui.session.state@integration.test", "ui-session-state-user", "Session State");
        var cookie = await _fixture.SignInAsync(user);
        await using var ui = ServedUnderUiPath(cookie, "UiSessionState-SignedIn");

        var state = await ui.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal(user.Email, state.User.FindFirst(ClaimTypes.Email)?.Value);
    }

    UiDriver ServedUnderUiPath(string? sessionCookie, string clientName)
    {
        return new UiDriver(
            _fixture.NoTimingBaseUrl,
            sessionCookie,
            clientName,
            new Uri(_fixture.NoTimingBaseUrl, "ui/")
        );
    }
}
