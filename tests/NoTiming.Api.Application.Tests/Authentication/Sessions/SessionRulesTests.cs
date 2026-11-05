using NoTiming.Api.Application.Authentication.Sessions;

namespace NoTiming.Api.Application.Tests.Authentication.Sessions;

public sealed class SessionRulesTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldExtend_WhenExtendedLessThanAnHourAgo_ShouldReturnFalse()
    {
        Assert.False(SessionRules.ShouldExtend(lastExtendedUtc: Now.AddMinutes(-59), now: Now));
    }

    [Fact]
    public void ShouldExtend_WhenExtendedAnHourAgo_ShouldReturnTrue()
    {
        Assert.True(SessionRules.ShouldExtend(lastExtendedUtc: Now.AddHours(-1), now: Now));
    }

    [Fact]
    public void ExpiresAt_WhenExtendedNow_ShouldBeThirtyDaysAhead()
    {
        Assert.Equal(Now.AddDays(30), SessionRules.ExpiresAt(Now));
    }

    [Fact]
    public void IsLive_WhenExpiryHasPassed_ShouldReturnFalse()
    {
        Assert.False(SessionRules.IsLive(expiresAtUtc: Now.AddSeconds(-1), now: Now));
    }

    [Fact]
    public void IsLive_WhenExpiryIsAhead_ShouldReturnTrue()
    {
        Assert.True(SessionRules.IsLive(expiresAtUtc: Now.AddSeconds(1), now: Now));
    }
}
