using System.Xml.Linq;
using Not.Domain.Exceptions;
using NoTiming.Console.Features.Rankings.Fei;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The FEI export of the Console (#646): the Results of an Event as the XML that the FEI takes. It is a pure transformation of
/// what the Results hold, so it is tried on Results that are composed here, and read back as the XML it makes.
/// </summary>
public sealed class FeiExportTests
{
    static readonly Guid EVENT = TestId.Of(10);

    [Fact]
    public void An_Event_with_two_configured_Rankings_is_one_Show_with_an_Endurance_Event_for_each_FEI_Event_ID()
    {
        var first = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01"
        );
        var second = RankingOf(
            2,
            "CEI 2*",
            feiEventId: "FEI-EVENT-2",
            feiEventCode: "CEI2",
            feiCompetitionId: "FEI-COMP-2",
            schedule: "02"
        );

        var document = new FeiExport().Create(
            EventOf(),
            [first.Result, second.Result],
            [first.Ranking, second.Ranking]
        );

        Assert.Equal("application/xml", document.ContentType);
        Assert.Equal("fei-export-Integration-FEI-Show.xml", document.FileName);
        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        var show = xml.Root.Element(ns + "EventResult")!.Element(ns + "Show")!;
        Assert.Equal("FEI-SHOW-1", show.Attribute("FEIID")!.Value);
        Assert.Collection(
            show.Elements(ns + "EnduranceEvent"),
            one =>
            {
                Assert.Equal("FEI-EVENT-1", one.Attribute("FEIID")!.Value);
                Assert.Equal("CEI1", one.Attribute("Code")!.Value);
                var competition = Assert.Single(one.Element(ns + "Competitions")!.Elements(ns + "Competition"));
                Assert.Equal("FEI-COMP-1", competition.Attribute("FEIID")!.Value);
                var participation = Assert.Single(
                    competition.Element(ns + "ParticipationList")!.Elements(ns + "Participation")
                );
                var athlete = participation.Element(ns + "Athlete")!;
                Assert.Equal("Athlete", athlete.Attribute("FirstName")!.Value);
                Assert.Equal("1", athlete.Attribute("FamilyName")!.Value);
                Assert.Equal("100001", athlete.Attribute("FEIID")!.Value);
                Assert.Equal("1", participation.Element(ns + "Position")!.Attribute("Rank")!.Value);
                Assert.Equal("Horse 1", participation.Element(ns + "Horse")!.Attribute("Name")!.Value);
            },
            two =>
            {
                Assert.Equal("FEI-EVENT-2", two.Attribute("FEIID")!.Value);
                Assert.Equal("CEI2", two.Attribute("Code")!.Value);
            }
        );
    }

    [Fact]
    public void Rankings_of_one_FEI_Event_share_its_Endurance_Event_and_are_its_Competitions()
    {
        var first = RankingOf(
            1,
            "Seniors",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01"
        );
        var second = RankingOf(
            2,
            "Juniors",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-2",
            schedule: "02"
        );

        var document = new FeiExport().Create(
            EventOf(),
            [second.Result, first.Result],
            [first.Ranking, second.Ranking]
        );

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        var events = xml.Descendants(ns + "EnduranceEvent").ToList();
        var single = Assert.Single(events);
        Assert.Equal(2, single.Element(ns + "Competitions")!.Elements(ns + "Competition").Count());
    }

    [Fact]
    public void A_Ranking_with_none_of_the_FEI_settings_is_left_out_of_the_export()
    {
        var configured = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01"
        );
        var plain = RankingOf(
            2,
            "Local",
            feiEventId: null,
            feiEventCode: null,
            feiCompetitionId: null,
            schedule: null,
            rule: null
        );

        var document = new FeiExport().Create(
            EventOf(),
            [configured.Result, plain.Result],
            [configured.Ranking, plain.Ranking]
        );

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        Assert.Single(xml.Descendants(ns + "Competition"));
        Assert.True(FeiExport.IsConfigured(configured.Ranking));
        Assert.False(FeiExport.IsConfigured(plain.Ranking));
    }

    [Fact]
    public void A_Ranking_with_some_of_the_FEI_settings_is_a_mistake_that_names_what_is_missing()
    {
        var partial = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: null,
            feiCompetitionId: null,
            schedule: "01"
        );

        var mistake = Assert.Throws<DomainException>(
            () => new FeiExport().Create(EventOf(), [partial.Result], [partial.Ranking])
        );

        Assert.Contains("missing FEI export configuration", mistake.Message);
        Assert.Contains("FEI Event Code", mistake.Message);
        Assert.Contains("FEI Competition ID", mistake.Message);
        Assert.DoesNotContain("FEI Event ID", mistake.Message);
        Assert.False(FeiExport.IsConfigured(partial.Ranking));
    }

    [Fact]
    public void An_Event_with_no_Ranking_configured_for_the_FEI_says_so()
    {
        var plain = RankingOf(
            1,
            "Local",
            feiEventId: null,
            feiEventCode: null,
            feiCompetitionId: null,
            schedule: null,
            rule: null
        );

        var mistake = Assert.Throws<DomainException>(
            () => new FeiExport().Create(EventOf(), [plain.Result], [plain.Ranking])
        );

        Assert.Contains("No rankings are configured", mistake.Message);
    }

    [Fact]
    public void A_Participation_without_the_FEI_ID_of_its_Athlete_or_its_Horse_is_named_by_its_number()
    {
        var withoutHorse = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01",
            horseFeiId: null
        );

        var mistake = Assert.Throws<DomainException>(
            () => new FeiExport().Create(EventOf(), [withoutHorse.Result], [withoutHorse.Ranking])
        );

        Assert.Contains("Athlete/Horse FEI ID", mistake.Message);
        Assert.Contains("1", mistake.Message);
    }

    [Fact]
    public void An_Event_without_a_Show_ID_is_refused_before_anything_is_made()
    {
        var ranking = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01"
        );

        var noShow = Assert.Throws<DomainException>(
            () => new FeiExport().Create(EventOf(feiShowId: null), [ranking.Result], [ranking.Ranking])
        );

        Assert.Contains("Show FEI ID", noShow.Message);
    }

    [Fact]
    public void A_Participation_that_was_eliminated_has_no_total_and_its_elimination_code_in_its_last_Phase()
    {
        var started = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01",
            eliminated: new Withdrawn()
        );

        var document = new FeiExport().Create(EventOf(), [started.Result], [started.Ranking]);

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        var participation = xml.Descendants(ns + "Participation").Single();
        Assert.Equal("WD", participation.Element(ns + "Position")!.Attribute("Status")!.Value);
        Assert.Null(participation.Element(ns + "Total"));
        Assert.Equal(
            "WD",
            participation.Descendants(ns + "VetInspection").Single().Attribute("EliminationCode")!.Value
        );
    }

    static EventInformation EventOf(string? feiShowId = "FEI-SHOW-1")
    {
        var start = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        return new EventInformation(
            new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG"),
            "Integration FEI Show",
            "Sofia",
            new EventSpan(start, start.AddDays(1)),
            feiShowId,
            id: EVENT
        );
    }

    static (Ranking Ranking, Result Result) RankingOf(
        int number,
        string name,
        string? feiEventId,
        string? feiEventCode,
        string? feiCompetitionId,
        string? schedule,
        string? rule = "E Comp",
        string? horseFeiId = "200001",
        Eliminated? eliminated = null
    )
    {
        var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(
            "Athlete " + number,
            null,
            country,
            null,
            (100000 + number).ToString(),
            TestId.Of(100 + number)
        );
        var horse = new Horse("Horse " + number, null, horseFeiId, TestId.Of(200 + number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(300 + number));
        var start = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var phase = new Phase(
            "GATE1",
            20,
            40,
            null,
            CompetitionRuleset.FEI,
            true,
            null,
            Timestamp.Create(start),
            Timestamp.Create(start.AddHours(2)),
            Timestamp.Create(start.AddHours(2).AddMinutes(10)),
            null,
            false,
            false,
            false
        );
        var participation = new Participation(
            ParticipationCategory.Senior,
            new Competition(name, CompetitionRuleset.FEI),
            combination,
            new PhaseCollection([phase]),
            eliminated,
            eventId: EVENT,
            id: TestId.Of(400 + number)
        );
        var ranking = new Ranking(
            name,
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            feiEventId,
            feiEventCode,
            feiCompetitionId,
            rule,
            schedule,
            [new RankingEntry(participation.Id, false, rank: 1)], // a Ranking that is final: its placings are stored
            EVENT,
            TestId.Of(500 + number)
        );
        return (ranking, new Result(ranking, [participation]));
    }
}
