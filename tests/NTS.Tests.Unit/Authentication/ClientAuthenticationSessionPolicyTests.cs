using Not.Application.Authentication.Provider;

namespace NTS.Authentication.Tests;

public class ClientAuthenticationSessionPolicyTests
{
    [Fact]
    public void Resolve_session_lifetime_defaults_to_24_hours_when_not_configured()
    {
        var settings = new NClientAuthenticationSettings();

        Assert.Equal(TimeSpan.FromHours(24), NClientAuthenticationSessionPolicy.ResolveSessionLifetime(settings));
    }

    [Fact]
    public void Resolve_session_lifetime_uses_configured_value()
    {
        var settings = new NClientAuthenticationSettings { SessionLifetime = TimeSpan.FromHours(12) };

        Assert.Equal(TimeSpan.FromHours(12), NClientAuthenticationSessionPolicy.ResolveSessionLifetime(settings));
    }

    [Fact]
    public void Is_session_active_returns_true_inside_the_window()
    {
        var startedAt = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddHours(23).AddMinutes(59);
        var settings = new NClientAuthenticationSettings { SessionLifetime = TimeSpan.FromHours(24) };

        Assert.True(NClientAuthenticationSessionPolicy.IsSessionActive(startedAt, settings, now));
    }

    [Fact]
    public void Is_session_active_returns_false_after_the_window()
    {
        var startedAt = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddHours(24).AddSeconds(1);
        var settings = new NClientAuthenticationSettings { SessionLifetime = TimeSpan.FromHours(24) };

        Assert.False(NClientAuthenticationSessionPolicy.IsSessionActive(startedAt, settings, now));
    }
}
