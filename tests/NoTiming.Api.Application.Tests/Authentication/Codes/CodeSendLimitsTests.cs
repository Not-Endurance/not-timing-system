using NoTiming.Api.Application.Authentication.Codes;

namespace NoTiming.Api.Application.Tests.Authentication.Codes;

public sealed class CodeSendLimitsTests
{
    // 10:20 UTC: forty minutes are left in the hour bucket.
    static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 20, 0, TimeSpan.Zero);

    [Fact]
    public void Decide_WhenNothingSentYet_ShouldAllow()
    {
        var decision = CodeSendLimits.Decide(new SendHistory(), Now);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Decide_WhenAddressSentWithinTheCooldown_ShouldRefuseWithTheRemainingCooldown()
    {
        var history = new SendHistory { LastSentForAddressUtc = Now.AddSeconds(-20) };

        var decision = CodeSendLimits.Decide(history, Now);

        Assert.False(decision.Allowed);
        Assert.Equal(SendLimitScope.Cooldown, decision.Scope);
        Assert.Equal(TimeSpan.FromSeconds(40), decision.RetryAfter);
    }

    [Fact]
    public void Decide_WhenAddressSentFiveCodesThisHour_ShouldRefuseForTheAddress()
    {
        var history = new SendHistory { ForAddressThisHour = 5 };

        var decision = CodeSendLimits.Decide(history, Now);

        Assert.False(decision.Allowed);
        Assert.Equal(SendLimitScope.Address, decision.Scope);
    }

    [Fact]
    public void Decide_WhenIpAddressSentTenCodesThisHour_ShouldRefuseForTheIpAddress()
    {
        var history = new SendHistory { ForIpThisHour = 10 };

        var decision = CodeSendLimits.Decide(history, Now);

        Assert.False(decision.Allowed);
        Assert.Equal(SendLimitScope.IpAddress, decision.Scope);
    }

    [Fact]
    public void Decide_WhenHundredCodesSentOverallThisHour_ShouldRefuseForAllRequests()
    {
        var history = new SendHistory { OverallThisHour = 100 };

        var decision = CodeSendLimits.Decide(history, Now);

        Assert.False(decision.Allowed);
        Assert.Equal(SendLimitScope.Overall, decision.Scope);
    }

    [Fact]
    public void Decide_WhenHourlyLimitReached_ShouldRetryWhenTheNextHourBegins()
    {
        var history = new SendHistory { ForAddressThisHour = 5 };

        var decision = CodeSendLimits.Decide(history, Now);

        Assert.Equal(TimeSpan.FromMinutes(40), decision.RetryAfter);
    }

    [Fact]
    public void Decide_WhenFourCodesSentThisHourAndCooldownHasPassed_ShouldAllow()
    {
        var history = new SendHistory { ForAddressThisHour = 4, LastSentForAddressUtc = Now.AddSeconds(-61) };

        var decision = CodeSendLimits.Decide(history, Now);

        Assert.True(decision.Allowed);
    }
}
