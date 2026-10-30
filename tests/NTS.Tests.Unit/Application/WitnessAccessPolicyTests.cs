using NoTiming.Ui;
using NoTiming.Ui.Features;

namespace NTS.Tests.Unit.Application;

/// <summary>Where the home address leads (#630, ADR-0007): a page of the Live Event the app follows, by whether the person may send Snapshots.</summary>
public sealed class WitnessAccessPolicyTests
{
    static readonly Guid EVENT = TestId.Of(7);

    [Fact]
    public void A_person_who_may_send_Snapshots_lands_on_the_Snapshot_page_of_the_Live_Event()
    {
        var route = WitnessAccessPolicy.ResolveHomeRoute(canSendSnapshots: true, EVENT);

        Assert.Equal(Routes.Of(Routes.EVENT_SNAPSHOT_PAGE, EVENT), route);
    }

    [Fact]
    public void Anybody_else_lands_on_the_Performance_page_of_the_Live_Event()
    {
        var route = WitnessAccessPolicy.ResolveHomeRoute(canSendSnapshots: false, EVENT);

        Assert.Equal(Routes.Of(Routes.EVENT_PERFORMANCE_PAGE, EVENT), route);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void With_no_Live_Event_there_is_no_page_of_one_and_the_person_stays_at_the_home_address(bool canSendSnapshots)
    {
        Assert.Equal(Routes.HOME, WitnessAccessPolicy.ResolveHomeRoute(canSendSnapshots, null));
    }
}
