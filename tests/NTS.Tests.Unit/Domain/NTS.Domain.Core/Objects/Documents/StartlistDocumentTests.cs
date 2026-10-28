using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Core.Objects.Documents;

public sealed class StartlistDocumentTests
{
    [Fact]
    public void The_starts_of_every_stage_are_on_the_sheet_by_the_time_they_start_whether_they_are_over_or_to_come()
    {
        var now = DateTimeOffset.Now;
        var over = now.AddHours(-3);
        var toCome = now.AddHours(1);
        var participations = new[]
        {
            Participation(3, Phase(toCome, isFinal: true)),
            Participation(1, Phase(over, isFinal: true)),
            Participation(2, Phase(over, isFinal: true)), // starts with the first one: by number then
            Participation(
                4,
                Phase(over.AddMinutes(30), arrive: over.AddHours(1), present: over.AddHours(1.1)),
                Phase(null, isFinal: true)
            ),
        };

        var document = StartlistDocument.Compose(participations, Event(), []);

        var stage = document.Stages.Single(x => x.Number == 1);
        Assert.Equal([1, 2, 4, 3], stage.Starters.Select(x => x.Number));
        var second = Assert.Single(document.Stages, x => x.Number == 2);
        Assert.Equal([4], second.Starters.Select(x => x.Number)); // its Phase is out, so it starts when the Phase before it was out
    }

    [Fact]
    public void A_start_that_has_no_time_is_not_on_the_sheet_and_the_stages_are_in_order()
    {
        var now = DateTimeOffset.Now;
        var participations = new[]
        {
            Participation(1, Phase(now.AddHours(-1)), Phase(null), Phase(null, isFinal: true)),
            Participation(2, Phase(now.AddHours(-1)), Phase(now.AddHours(2), isFinal: true)),
        };

        var document = StartlistDocument.Compose(participations, Event(), []);

        Assert.Equal([1, 2], document.Stages.Select(x => x.Number));
        Assert.Equal([1, 2], document.Stages[0].Starters.Select(x => x.Number));
        Assert.Equal([2], document.Stages[1].Starters.Select(x => x.Number));
    }

    [Fact]
    public void Starts_at_the_same_time_are_by_number_whether_one_of_them_is_over_and_the_other_is_to_come()
    {
        var start = DateTimeOffset.Now.AddHours(1);
        var participations = new[]
        {
            // its Phase is complete, so its start is over: the sheet reads it before the ones that are to come
            Participation(
                5,
                Phase(start, arrive: start.AddHours(1), present: start.AddHours(1.1)),
                Phase(null, isFinal: true)
            ),
            Participation(3, Phase(start, isFinal: true)),
        };

        var document = StartlistDocument.Compose(participations, Event(), []);

        Assert.Equal([3, 5], document.Stages.Single(x => x.Number == 1).Starters.Select(x => x.Number));
    }

    [Fact]
    public void The_stages_are_in_order_when_the_first_start_to_be_read_is_of_a_later_stage()
    {
        var now = DateTimeOffset.Now;
        var participations = new[]
        {
            // its first Phase has no time, so its second one, which is over, is what the sheet reads first
            Participation(1, Phase(null), Phase(now.AddHours(-2), isFinal: true)),
            Participation(2, Phase(now.AddHours(1), isFinal: true)),
        };

        var document = StartlistDocument.Compose(participations, Event(), []);

        Assert.Equal([1, 2], document.Stages.Select(x => x.Number));
        Assert.Equal([2], document.Stages[0].Starters.Select(x => x.Number));
        Assert.Equal([1], document.Stages[1].Starters.Select(x => x.Number));
    }

    [Fact]
    public void The_sheet_is_headed_by_the_Event_and_follows_the_ruleset_of_its_first_Participation()
    {
        var official = new Official("Ivan Ivanov", null, OfficialRole.ChiefSteward, TestId.Of(10), TestId.Of(30));
        var document = StartlistDocument.Compose(
            [
                Participation(2, Phase(DateTimeOffset.Now, isFinal: true), CompetitionRuleset.FEI),
                Participation(1, Phase(DateTimeOffset.Now, isFinal: true)),
            ],
            Event(),
            [official]
        );

        Assert.Equal("Event", document.Header.Title);
        Assert.Equal("Location", document.Header.Location);
        Assert.Equal(CompetitionRuleset.Regional, document.Header.Ruleset);
        Assert.Same(official, document.Header.ChiefSteward);
    }

    [Fact]
    public void An_Event_without_Participations_has_a_sheet_without_stages()
    {
        var document = StartlistDocument.Compose([], Event(), []);

        Assert.Empty(document.Stages);
        Assert.Equal("Event", document.Header.Title);
    }

    static EventInformation Event()
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        return new EventInformation(
            country,
            "Event",
            "Location",
            new EventSpan(DateTimeOffset.Now.Date, DateTimeOffset.Now.Date.AddDays(1)),
            null,
            id: TestId.Of(10)
        );
    }

    static Participation Participation(
        int number,
        Phase first,
        CompetitionRuleset ruleset = CompetitionRuleset.Regional
    )
    {
        return Participation(number, ruleset, first);
    }

    static Participation Participation(int number, Phase first, Phase second)
    {
        return Participation(number, CompetitionRuleset.Regional, first, second);
    }

    static Participation Participation(int number, Phase first, Phase second, Phase third)
    {
        return Participation(number, CompetitionRuleset.Regional, first, second, third);
    }

    static Participation Participation(int number, CompetitionRuleset ruleset, params Phase[] phases)
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete($"Athlete {number}", null, country, null, null, TestId.Of(100 + number));
        var horse = new Horse($"Horse {number}", null, null, TestId.Of(200 + number));
        var combination = new Combination(number, athlete, horse, null, "40", null, null, TestId.Of(300 + number));
        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", ruleset),
            combination,
            new PhaseCollection(phases),
            null,
            eventId: TestId.Of(10)
        );
    }

    static Phase Phase(
        DateTimeOffset? start,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        bool isFinal = false
    )
    {
        return new Phase(
            "GATE",
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.Regional,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            null,
            false,
            false,
            false
        );
    }
}
