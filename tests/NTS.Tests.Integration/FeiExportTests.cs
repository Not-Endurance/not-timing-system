using System.Xml.Linq;
using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Judge.Contracts.Features.Core.Rankings.FeiExport;
using NTS.Tests.Integration.Drivers;
using CoreCompetition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration;

public sealed class FeiExportTests
{
    [Fact]
    public async Task Create_GeneratesSingleShowWithMultipleEnduranceEvents()
    {
        await using var judge = new JudgeDriver(new Uri("http://127.0.0.1:1"), new Uri("http://127.0.0.1:2"));
        var service = judge.GetRequiredService<IFeiExportService>();
        var eventInformation = CreateEventInformation();
        var rankings = new[]
        {
            CreateRanking("CEI 1*", "FEI-EVENT-1", "CEI1", "FEI-COMP-1", "01", 1),
            CreateRanking("CEI 2*", "FEI-EVENT-2", "CEI2", "FEI-COMP-2", "02", 2),
        };

        var document = service.Create(eventInformation, rankings);

        Assert.Equal("application/xml", document.ContentType);
        Assert.Equal("fei-export-Integration-FEI-Show.xml", document.FileName);

        var xml = XDocument.Parse(document.Content);
        var ns = xml.Root!.Name.Namespace;
        var show = xml.Root.Element(ns + "EventResult")!.Element(ns + "Show")!;
        Assert.Equal("FEI-SHOW-1", show.Attribute("FEIID")!.Value);

        var enduranceEvents = show.Elements(ns + "EnduranceEvent").ToArray();
        Assert.Collection(
            enduranceEvents,
            first =>
            {
                Assert.Equal("FEI-EVENT-1", first.Attribute("FEIID")!.Value);
                Assert.Equal("CEI1", first.Attribute("Code")!.Value);
                var competition = Assert.Single(first.Element(ns + "Competitions")!.Elements(ns + "Competition"));
                Assert.Equal("FEI-COMP-1", competition.Attribute("FEIID")!.Value);
            },
            second =>
            {
                Assert.Equal("FEI-EVENT-2", second.Attribute("FEIID")!.Value);
                Assert.Equal("CEI2", second.Attribute("Code")!.Value);
                var competition = Assert.Single(second.Element(ns + "Competitions")!.Elements(ns + "Competition"));
                Assert.Equal("FEI-COMP-2", competition.Attribute("FEIID")!.Value);
            }
        );
    }

    [Fact]
    public async Task Create_RejectsPartialFeiRankingConfiguration()
    {
        await using var judge = new JudgeDriver(new Uri("http://127.0.0.1:1"), new Uri("http://127.0.0.1:2"));
        var service = judge.GetRequiredService<IFeiExportService>();
        var ranking = CreateRanking(
            "CEI 1*",
            feiEventId: "FEI-EVENT-1",
            feiEventCode: null,
            feiCompetitionId: null,
            feiScheduleNumber: null,
            number: 1
        );

        var ex = Assert.Throws<DomainException>(() => service.Create(CreateEventInformation(), [ranking]));

        Assert.Contains("missing FEI export configuration", ex.Message);
        Assert.Contains("FEI Event Code", ex.Message);
        Assert.Contains("FEI Competition ID", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsMissingParticipantFeiIds()
    {
        await using var judge = new JudgeDriver(new Uri("http://127.0.0.1:1"), new Uri("http://127.0.0.1:2"));
        var service = judge.GetRequiredService<IFeiExportService>();
        var ranking = CreateRanking("CEI 1*", "FEI-EVENT-1", "CEI1", "FEI-COMP-1", "01", 1, horseFeiId: null);

        var ex = Assert.Throws<DomainException>(() => service.Create(CreateEventInformation(), [ranking]));

        Assert.Contains("Athlete/Horse FEI ID", ex.Message);
        Assert.Contains("1", ex.Message);
    }

    static EventInformation CreateEventInformation()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var start = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        return new EventInformation(
            country,
            "Integration FEI Show",
            "Sofia",
            new EventSpan(start, start.AddDays(1)),
            "FEI-SHOW-1",
            1001
        );
    }

    static Ranking CreateRanking(
        string name,
        string? feiEventId,
        string? feiEventCode,
        string? feiCompetitionId,
        string? feiScheduleNumber,
        int number,
        string? athleteFeiId = "100001",
        string? horseFeiId = "200001"
    )
    {
        var participation = CreateParticipation(number, athleteFeiId, horseFeiId);
        var entries = new[] { new RankingEntry(participation, 1, false, 7000 + number) };
        return new Ranking(
            name,
            CompetitionRuleset.FEI,
            CompetitionType.Qualification,
            ParticipationCategory.Senior,
            feiEventId,
            feiEventCode,
            feiCompetitionId,
            "E Comp",
            feiScheduleNumber,
            entries,
            participation.EventId,
            8000 + number
        );
    }

    static Participation CreateParticipation(int number, string? athleteFeiId, string? horseFeiId)
    {
        const int eventId = 1001;
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete(new Person(["FEI", $"Rider{number}"]), country, null, athleteFeiId, 100 + number);
        var horse = new Horse($"FEI Horse {number}", horseFeiId, 200 + number);
        var combination = new Combination(number, athlete, horse, null, "40", null, null, 300 + number);
        var competition = new CoreCompetition("CEI 1*", CompetitionRuleset.FEI, CompetitionType.Qualification);
        var start = new Timestamp(new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero));
        var phase = new Phase(
            "GATE1/40",
            40,
            40,
            rest: null,
            CompetitionRuleset.FEI,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: start,
            arriveTime: null,
            presentTime: null,
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: 400 + number
        );

        return new Participation(
            ParticipationCategory.Senior,
            competition,
            combination,
            new PhaseCollection([phase]),
            notQualified: null,
            eventId,
            id: 500 + number
        );
    }
}
