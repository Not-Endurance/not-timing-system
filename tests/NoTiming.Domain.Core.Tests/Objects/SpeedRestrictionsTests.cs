using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Tests.Objects;

public sealed class SpeedRestrictionsTests
{
    [Fact]
    public void Strictest_WhenBothLevelsSetMaximum_ShouldKeepLowerMaximum()
    {
        var competition = new SpeedRestrictions(null, null, null, null, null, null, null, 16);
        var participation = new SpeedRestrictions(null, null, null, null, null, null, null, 12);

        var strictest = SpeedRestrictions.Strictest(competition, participation);

        Assert.Equal(12, strictest.PhaseAverageSpeedMax);
    }

    [Fact]
    public void Strictest_WhenBothLevelsSetMinimum_ShouldKeepHigherMinimum()
    {
        var competition = new SpeedRestrictions(null, null, null, null, null, null, 10, null);
        var participation = new SpeedRestrictions(null, null, null, null, null, null, 12, null);

        var strictest = SpeedRestrictions.Strictest(competition, participation);

        Assert.Equal(12, strictest.PhaseAverageSpeedMin);
    }

    [Fact]
    public void Strictest_WhenOnlyParticipationSetsLimit_ShouldKeepIt()
    {
        var participation = new SpeedRestrictions(16, null, null, null, null, null, null, null);

        var strictest = SpeedRestrictions.Strictest(SpeedRestrictions.None, participation);

        Assert.Equal(16, strictest.LoopSpeedMin);
        Assert.Null(strictest.LoopSpeedMax);
    }

    [Fact]
    public void Strictest_WhenNeitherLevelSetsLimit_ShouldBeNone()
    {
        var strictest = SpeedRestrictions.Strictest(null, null);

        Assert.Equal(SpeedRestrictions.None, strictest);
    }

    [Fact]
    public void Strictest_WhenMetricsDiffer_ShouldKeepEachMetricSeparately()
    {
        var competition = new SpeedRestrictions(null, 18, null, null, 10, null, null, null);
        var participation = new SpeedRestrictions(null, 16, null, null, null, 15, null, null);

        var strictest = SpeedRestrictions.Strictest(competition, participation);

        Assert.Equal(16, strictest.LoopSpeedMax);
        Assert.Equal(10, strictest.LoopAverageSpeedMin);
        Assert.Equal(15, strictest.LoopAverageSpeedMax);
    }
}
