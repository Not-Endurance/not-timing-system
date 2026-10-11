using NoTiming.Console;
using NoTiming.Console.Features.Navigation;
using NoTiming.Ui;
using NTS.Contracts.Features.Console;
using NTS.Domain.Access;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The pages of an Event that the Console offers a person (#646): the ones the Event at its stage has for them and no others,
/// with the Console first, and the page they are on shown as such.
/// </summary>
public sealed class EventLinksTests
{
    static readonly Guid EVENT = TestId.Of(7);

    [Fact]
    public void An_Event_that_has_not_started_offers_its_Setup_to_the_person_who_runs_it()
    {
        var links = EventLinks.For(Row(EventStage.Unstarted, true), ConsoleRoutes.EVENT_SETUP_PAGE);

        Assert.Equal([Console_string, Setup_string], links.Select(x => x.Text));
        Assert.Equal(["/console", $"/console/events/{EVENT}/setup"], links.Select(x => x.Route));
        Assert.Equal([false, true], links.Select(x => x.IsCurrent));
    }

    [Fact]
    public void A_Live_Event_offers_its_pages_to_the_person_who_runs_it_and_the_pages_the_viewers_have()
    {
        var links = EventLinks.For(Row(EventStage.Live, true), ConsoleRoutes.EVENT_HANDOUTS_PAGE);

        Assert.Equal(
            [
                Console_string,
                Dashboard_string,
                Startlist_string,
                Ranklist_string,
                Handouts_string,
                Arrivelist_string,
                Presentlist_string,
            ],
            links.Select(x => x.Text)
        );
        Assert.Equal(
            [
                "/console",
                $"/console/events/{EVENT}/dashboard",
                $"/console/events/{EVENT}/startlist",
                $"/console/events/{EVENT}/rankings",
                $"/console/events/{EVENT}/handouts",
                $"/events/{EVENT}/arrivelist",
                $"/events/{EVENT}/presentlist",
            ],
            links.Select(x => x.Route)
        );
        Assert.Equal(Handouts_string, Assert.Single(links, x => x.IsCurrent).Text);
    }

    [Theory]
    [InlineData(EventStage.Live)]
    [InlineData(EventStage.Historic)]
    public void An_Event_that_the_person_does_not_run_offers_its_Results_and_nothing_to_change(EventStage stage)
    {
        var links = EventLinks.For(Row(stage, false), ConsoleRoutes.HOME);

        Assert.Equal([Console_string, Ranklist_string], links.Select(x => x.Text));
        Assert.Equal(["/console", $"/console/events/{EVENT}/rankings"], links.Select(x => x.Route));
        Assert.True(links[0].IsCurrent);
    }

    [Fact]
    public void An_Event_that_has_not_started_offers_nothing_to_a_person_who_does_not_run_it()
    {
        var links = EventLinks.For(Row(EventStage.Unstarted, false), ConsoleRoutes.HOME);

        Assert.Equal([Console_string], links.Select(x => x.Text));
    }

    [Fact]
    public void A_Historic_Event_that_the_person_ran_offers_its_Results_only()
    {
        var links = EventLinks.For(Row(EventStage.Historic, true), ConsoleRoutes.EVENT_RANKINGS_PAGE);

        Assert.Equal([Console_string, Ranklist_string], links.Select(x => x.Text));
        Assert.Equal([false, true], links.Select(x => x.IsCurrent));
    }

    static ConsoleEvent Row(EventStage stage, bool isMainOperator)
    {
        return new ConsoleEvent
        {
            Id = EVENT,
            Name = "Event",
            Location = "Location",
            Stage = stage,
            IsMainOperator = isMainOperator,
        };
    }
}
