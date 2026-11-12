using NC.Server.Authentication.Sessions;
using Xunit;

namespace NC.Server.Tests.Authentication.Sessions;

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
}
