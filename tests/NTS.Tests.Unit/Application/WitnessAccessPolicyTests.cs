using NoTiming.Ui;
using NoTiming.Ui.Features;
using NTS.Contracts.Features.Access;

namespace NTS.Tests.Unit.Application;

/// <summary>Where the home address leads (#630, ADR-0007): a page of the Live Event the app follows, by what the person may do in it.</summary>
public sealed class WitnessAccessPolicyTests
{
    static readonly Guid EVENT = TestId.Of(7);

    [Fact]
    public void An_Official_lands_on_the_Snapshot_page_of_the_Live_Event()
    {
        var route = WitnessAccessPolicy.ResolveHomeRoute(WitnessAccessLevel.Official, EVENT);

        Assert.Equal(Routes.Of(Routes.EVENT_SNAPSHOT_PAGE, EVENT), route);
    }

    [Theory]
    [InlineData(WitnessAccessLevel.Unknown)]
    [InlineData(WitnessAccessLevel.Anonymous)]
    [InlineData(WitnessAccessLevel.Registered)]
    public void Anybody_else_lands_on_the_Performance_page_of_the_Live_Event(WitnessAccessLevel level)
    {
        var route = WitnessAccessPolicy.ResolveHomeRoute(level, EVENT);

        Assert.Equal(Routes.Of(Routes.EVENT_PERFORMANCE_PAGE, EVENT), route);
    }

    [Theory]
    [InlineData(WitnessAccessLevel.Anonymous)]
    [InlineData(WitnessAccessLevel.Registered)]
    [InlineData(WitnessAccessLevel.Official)]
    public void With_no_Live_Event_there_is_no_page_of_one_and_the_person_stays_at_the_home_address(
        WitnessAccessLevel level
    )
    {
        Assert.Equal(Routes.HOME, WitnessAccessPolicy.ResolveHomeRoute(level, null));
    }
}
