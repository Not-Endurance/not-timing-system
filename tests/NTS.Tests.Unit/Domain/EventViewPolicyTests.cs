using NTS.Domain.Access;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What the Core views show of an Event and whether they may change it, by the stage the Event is in (#630, ADR-0007).
/// A Live Event shows every Core view and can be written to by whoever the Api lets write; a Historic Event is a record:
/// it shows its Rankings and Results and the Participation detail, hides the views that are about the running Event (the
/// Startlist among them), and takes no write from a view whoever looks at it.
/// </summary>
public sealed class EventViewPolicyTests
{
    [Theory]
    [InlineData(CoreView.Startlist)]
    [InlineData(CoreView.Rankings)]
    [InlineData(CoreView.Results)]
    [InlineData(CoreView.ParticipationDetail)]
    [InlineData(CoreView.Handouts)]
    [InlineData(CoreView.Arrivelist)]
    [InlineData(CoreView.Presentlist)]
    [InlineData(CoreView.SnapshotCapture)]
    [InlineData(CoreView.Performance)]
    public void A_Live_Event_shows_every_Core_view(CoreView view)
    {
        Assert.True(EventViewPolicy.Shows(view, EventStage.Live));
    }

    [Theory]
    [InlineData(CoreView.Rankings, true)]
    [InlineData(CoreView.Results, true)]
    [InlineData(CoreView.ParticipationDetail, true)]
    [InlineData(CoreView.Startlist, false)]
    [InlineData(CoreView.Handouts, false)]
    [InlineData(CoreView.Arrivelist, false)]
    [InlineData(CoreView.Presentlist, false)]
    [InlineData(CoreView.SnapshotCapture, false)]
    [InlineData(CoreView.Performance, false)]
    public void A_Historic_Event_shows_its_record_and_hides_what_is_about_the_running_Event(CoreView view, bool shown)
    {
        Assert.Equal(shown, EventViewPolicy.Shows(view, EventStage.Historic));
    }

    [Fact]
    public void An_Event_that_has_not_started_shows_nothing_because_there_is_nothing_of_it_to_see_yet()
    {
        Assert.All(Enum.GetValues<CoreView>(), view => Assert.False(EventViewPolicy.Shows(view, EventStage.Unstarted)));
    }

    [Theory]
    [InlineData(EventStage.Live, true, true)]
    [InlineData(EventStage.Live, false, false)]
    [InlineData(EventStage.Historic, true, false)]
    [InlineData(EventStage.Historic, false, false)]
    [InlineData(EventStage.Unstarted, true, false)]
    [InlineData(EventStage.Unstarted, false, false)]
    public void A_view_may_write_to_an_Event_that_is_Live_and_only_for_whoever_is_permitted(
        EventStage stage,
        bool permitted,
        bool canWrite
    )
    {
        Assert.Equal(canWrite, EventViewPolicy.CanWrite(stage, permitted));
    }

    [Fact]
    public void A_view_the_policy_has_no_row_for_is_a_mistake_of_the_caller_and_not_a_refusal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EventViewPolicy.Shows((CoreView)99, EventStage.Live));
    }
}
