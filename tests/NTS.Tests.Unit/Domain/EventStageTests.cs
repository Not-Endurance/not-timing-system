using NTS.Domain.Access;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// Where an Event is in its life is a rule (ADR-0007), not a field: it is not started until it starts, Live until the end
/// of its last day, and Historic from then on, with no grace. The end is an instant: the stored end of the last day,
/// which is 23:59:59 in the Event's own offset, and which the rule compares with a clock it is given.
/// </summary>
public sealed class EventStageTests
{
    static readonly DateTimeOffset END = new(2026, 5, 2, 23, 59, 59, TimeSpan.FromHours(3));

    [Fact]
    public void An_Event_that_has_not_started_is_not_started_whatever_its_end_and_the_time()
    {
        Assert.Equal(EventStage.Unstarted, EventStageRule.Of(false, null, END.AddDays(-30)));
        Assert.Equal(EventStage.Unstarted, EventStageRule.Of(false, END, END.AddDays(-30)));
        Assert.Equal(EventStage.Unstarted, EventStageRule.Of(false, END, END.AddDays(30)));
    }

    [Fact]
    public void A_started_Event_is_Live_until_the_second_before_the_end_of_its_last_day()
    {
        Assert.Equal(EventStage.Live, EventStageRule.Of(true, END, END.AddDays(-5)));
        Assert.Equal(EventStage.Live, EventStageRule.Of(true, END, END.AddSeconds(-1)));
    }

    [Fact]
    public void A_started_Event_is_Historic_from_the_end_of_its_last_day_with_no_grace()
    {
        Assert.Equal(EventStage.Historic, EventStageRule.Of(true, END, END));
        Assert.Equal(EventStage.Historic, EventStageRule.Of(true, END, END.AddSeconds(1)));
        Assert.Equal(EventStage.Historic, EventStageRule.Of(true, END, END.AddDays(40)));
    }

    [Fact]
    public void The_end_is_an_instant_so_a_clock_in_another_offset_decides_the_same()
    {
        var sameInstantInUtc = END.ToUniversalTime();

        Assert.Equal(EventStage.Live, EventStageRule.Of(true, END, sameInstantInUtc.AddSeconds(-1)));
        Assert.Equal(EventStage.Historic, EventStageRule.Of(true, END, sameInstantInUtc));
        Assert.Equal(EventStage.Historic, EventStageRule.Of(true, sameInstantInUtc, END.AddSeconds(1)));
    }

    [Fact]
    public void A_started_Event_with_no_end_is_taken_to_be_over_so_that_nothing_is_written_to_it()
    {
        Assert.Equal(EventStage.Historic, EventStageRule.Of(true, null, END.AddDays(-5)));
    }
}
