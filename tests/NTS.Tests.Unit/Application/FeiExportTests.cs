using System.Globalization;
using System.Xml.Linq;
using Not.Domain.Exceptions;
using NoTiming.Console.Features.Rankings.Fei;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
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
    const string EXPECTED = """
        <HorseSport xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.fei.org/Result">
          <Generated Software="NTS" SoftwareVersion="1.0.0" Organization="NotACompany" />
          <EventResult>
            <Show FEIID="FEI-SHOW-1" StartDate="2026-05-01" EndDate="2026-05-02">
              <Venue Name="Sofia" Country="BUL" />
              <EnduranceEvent FEIID="FEI-EVENT-1" Code="CEI1" NF="BUL" StartDate="2026-05-01" EndDate="2026-05-02">
                <Competitions>
                  <Competition FEIID="FEI-COMP-1" ScheduleCompetitionNr="01" Rule="E Comp 2" Name="CEI 1*" StartDate="2026-05-01">
                    <ParticipationList>
                      <Participation>
                        <Athlete FEIID="100001" FirstName="Athlete" FamilyName="1" CompetingFor="BUL" />
                        <Horse FEIID="200001" Name="Horse 1" />
                        <Complement BestCondition="false" />
                        <Position Status="R" Rank="1" />
                        <Phases>
                          <Day Number="1">
                            <Phase Number="1">
                              <Result PhaseAverageSpeed="10" PhaseTime="02:00:00" RecoveryTime="00:10:00" />
                            </Phase>
                          </Day>
                        </Phases>
                        <Total Time="02:00:00" AverageSpeed="10" />
                      </Participation>
                    </ParticipationList>
                  </Competition>
                </Competitions>
              </EnduranceEvent>
            </Show>
          </EventResult>
        </HorseSport>
        """;
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
        var blankShow = Assert.Throws<DomainException>(
            () => new FeiExport().Create(EventOf(feiShowId: "  "), [ranking.Result], [ranking.Ranking])
        );
        Assert.Contains("Show FEI ID", blankShow.Message);
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
        Assert.Null(participation.Element(ns + "Position")!.Attribute("Rank")); // it holds no placing, whatever the Ranking stored
        Assert.Null(participation.Element(ns + "Total"));
        Assert.Equal(
            "WD",
            participation.Descendants(ns + "VetInspection").Single().Attribute("EliminationCode")!.Value
        );
    }

    [Fact]
    public void The_file_says_what_the_Event_and_the_Ranking_hold_in_the_places_the_FEI_reads_them_from()
    {
        var one = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01",
            rule: "E Comp 2"
        );

        var document = new FeiExport().Create(EventOf(), [one.Result], [one.Ranking]);

        var xml = XDocument.Parse(document.Content);
        var generated = xml.Root!.Element(xml.Root.Name.Namespace + "Generated")!;
        var madeAt = DateTimeOffset.Parse(generated.Attribute("Date")!.Value, CultureInfo.InvariantCulture);
        Assert.InRange(madeAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        generated.Attribute("Date")!.Remove();
        // The Ranking has one Phase of 20 km that was ridden in two hours and then rested ten minutes at the vet gate: 10 km/h
        Assert.Equal(XDocument.Parse(EXPECTED).ToString(), xml.ToString());
    }

    [Fact]
    public void Rankings_of_the_same_FEI_Event_with_other_codes_are_Endurance_Events_by_the_ID_and_then_the_code()
    {
        var late = RankingOf(1, "Two", "FEI-EVENT-1", "CEI2", "FEI-COMP-1", "01");
        var early = RankingOf(2, "One", "FEI-EVENT-1", "CEI1", "FEI-COMP-2", "02");

        var document = new FeiExport().Create(EventOf(), [late.Result, early.Result], [late.Ranking, early.Ranking]);

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        Assert.Equal(["CEI1", "CEI2"], xml.Descendants(ns + "EnduranceEvent").Select(x => x.Attribute("Code")!.Value));
    }

    [Fact]
    public void The_file_is_named_by_the_Event_with_a_dash_for_whatever_is_not_a_letter_or_a_digit_and_none_at_the_ends()
    {
        var ranking = RankingOf(1, "CEI 1*", "FEI-EVENT-1", "CEI1", "FEI-COMP-1", "01");

        var named = new FeiExport().Create(EventOf(name: " (Spring) Cup! "), [ranking.Result], [ranking.Ranking]);
        var punctuation = new FeiExport().Create(EventOf(name: "***"), [ranking.Result], [ranking.Ranking]);

        Assert.Equal("fei-export-Spring--Cup.xml", named.FileName);
        Assert.Equal($"fei-export-{EVENT}.xml", punctuation.FileName); // there is no name in it to give the file
    }

    [Fact]
    public void The_Phases_of_a_day_are_in_its_Day_and_a_Participation_that_rode_two_days_has_two()
    {
        var ranking = RankingOf(
            1,
            "CEI 2*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI2",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01",
            phases: TwoDaysOfTwoPhases()
        );

        var document = new FeiExport().Create(EventOf(), [ranking.Result], [ranking.Ranking]);

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        var days = xml.Descendants(ns + "Day").ToList();
        Assert.Equal(["1", "2"], days.Select(x => x.Attribute("Number")!.Value));
        Assert.Equal(["1", "2"], days[0].Elements(ns + "Phase").Select(x => x.Attribute("Number")!.Value));
        Assert.Equal(["3", "4"], days[1].Elements(ns + "Phase").Select(x => x.Attribute("Number")!.Value));
        // 20 km in the 2 h 10 min of the ride and the wait for the vet gate is 9.23 km/h, and the last Phase has no vet gate
        var phases = xml.Descendants(ns + "Phase").Select(x => x.Element(ns + "Result")!).ToList();
        Assert.Equal(["9.23", "9.23", "9.23", "10"], phases.Select(x => x.Attribute("PhaseAverageSpeed")!.Value));
        Assert.Equal(
            ["02:10:00", "02:10:00", "02:10:00", "02:00:00"],
            phases.Select(x => x.Attribute("PhaseTime")!.Value)
        );
        Assert.Equal(
            ["00:10:00", "00:10:00", "00:10:00", "00:00:00"],
            phases.Select(x => x.Attribute("RecoveryTime")!.Value)
        );
        Assert.Empty(xml.Descendants(ns + "VetInspection"));
    }

    [Fact]
    public void An_elimination_is_in_the_last_Phase_the_Participation_rode_with_the_codes_it_failed_to_qualify_by()
    {
        var ranking = RankingOf(
            1,
            "CEI 2*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI2",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01",
            eliminated: new FailedToQualify([FailToQualifyCode.GA, FailToQualifyCode.ME]),
            phases: TwoDaysOfTwoPhases()
        );

        var document = new FeiExport().Create(EventOf(), [ranking.Result], [ranking.Ranking]);

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        Assert.Equal("FTQ", xml.Descendants(ns + "Position").Single().Attribute("Status")!.Value);
        var inspection = xml.Descendants(ns + "VetInspection").Single();
        Assert.Equal("GA ME", inspection.Attribute("EliminationCode")!.Value);
        Assert.Equal("4", inspection.Parent!.Attribute("Number")!.Value);
    }

    [Fact]
    public void A_Phase_that_has_no_start_is_not_in_the_file_and_the_ones_after_it_keep_their_numbers()
    {
        var start = new DateTimeOffset(2026, 5, 1, 6, 0, 0, TimeSpan.Zero);
        var withoutStart = new Phase(
            "GATE",
            20,
            40,
            40,
            CompetitionRuleset.FEI,
            false,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            false
        );
        var ranking = RankingOf(
            1,
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: "CEI1",
            feiCompetitionId: "FEI-COMP-1",
            schedule: "01",
            phases: [Leg(start), withoutStart, Leg(start.AddHours(5), isFinal: true)]
        );

        var document = new FeiExport().Create(EventOf(), [ranking.Result], [ranking.Ranking]);

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        Assert.Equal(["1", "3"], xml.Descendants(ns + "Phase").Select(x => x.Attribute("Number")!.Value));
    }

    /// <summary>Two days of two Phases: each is 20 km ridden in two hours, with ten minutes between the arrival and the vet gate.</summary>
    static Phase[] TwoDaysOfTwoPhases()
    {
        var first = new DateTimeOffset(2026, 5, 1, 6, 0, 0, TimeSpan.Zero);
        var second = first.AddDays(1);
        return
        [
            Leg(first),
            Leg(first.AddHours(2).AddMinutes(50)),
            Leg(second),
            Leg(second.AddHours(2).AddMinutes(50), isFinal: true),
        ];
    }

    static Phase Leg(DateTimeOffset start, bool isFinal = false)
    {
        return new Phase(
            "GATE",
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.FEI,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(start.AddHours(2)),
            isFinal ? null : Timestamp.Create(start.AddHours(2).AddMinutes(10)),
            null,
            false,
            false,
            false
        );
    }

    static EventInformation EventOf(string? feiShowId = "FEI-SHOW-1", string name = "Integration FEI Show")
    {
        var start = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        return new EventInformation(
            new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG"),
            name,
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
        Eliminated? eliminated = null,
        Phase[]? phases = null
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
            new PhaseCollection(phases ?? [phase]),
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
