using Newtonsoft.Json;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Features.Access;
using NTS.Contracts.HistoricEvents;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Judge.Contracts.Features.Core;
using NTS.Judge.Contracts.Features.Core.Rankings.FeiExport;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.EndToEndEventTests.Features;
using NTS.Tests.Integration.EndToEndEventTests.Helpers;
using NTS.Tests.Integration.Infrastructure;
using CoreAthlete = NTS.Domain.Core.Aggregates.Participations.Entities.Athlete;
using CoreCombination = NTS.Domain.Core.Aggregates.Participations.Entities.Combination;
using CoreHorse = NTS.Domain.Core.Aggregates.Participations.Entities.Horse;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;

namespace NTS.Tests.Integration.EndToEndEventTests;

[Collection(EndToEndEventCollection.Name)]
public sealed class CoreFeatureEndToEndTests
{
    readonly NtsIntegrationFixture _fixture;

    public CoreFeatureEndToEndTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public static IEnumerable<object[]> EventSnapshots =>
        EndToEndEventSnapshot.DiscoverNames().Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(EventSnapshots))]
    public async Task Event_snapshot_runs_end_to_end(string snapshotName)
    {
        var snapshot = EndToEndEventSnapshot.Load(snapshotName);
        await using var console = new ConsoleDriver(_fixture.ApiBaseUrl, _fixture.FunctionsBaseUrl);
        using var functionsApi = new FunctionsApiDriver(_fixture.FunctionsBaseUrl);
        using var print = new EndToEndPrintFeature(functionsApi);
        var configureEvent = new ConfigureEventFeature(console, functionsApi);
        var startEvent = new StartCoreEventFeature(console, functionsApi);

        await SeedOtherEventData(functionsApi, snapshot.EventId);

        var setup = await configureEvent.Execute(snapshot);
        var eventInformation = await startEvent.Execute(setup);
        await AssertStartedConfigureEventCannotBeUpdated(functionsApi, setup.SetupEvent);

        var startedDocuments = await ReadStartedDocumentsScopedToCurrentEvent(
            functionsApi,
            eventInformation,
            setup,
            snapshot
        );
        var startedParticipations = startedDocuments.Participations;
        var startedRankings = startedDocuments.Rankings;
        Assert.Equal(snapshot.Participations.Count, startedParticipations.Count);
        Assert.Equal(snapshot.Rankings.Count, startedRankings.Count);
        AssertStartedOperatorsMatchSetup(startedDocuments.Operators, setup.SetupEvent, eventInformation.Id);

        await using var witness = new ViewerDriver(
            _fixture.ApiBaseUrl,
            _fixture.FunctionsBaseUrl,
            setup.WitnessOperator,
            $"CoreEndToEndOperatorWitness-{snapshot.Name}"
        );
        await witness.Start();
        await witness.Connect(eventInformation);
        Assert.Equal(WitnessAccessLevel.Official, witness.AccessLevel);

        var phaseWaves = CreatePhaseWaves(snapshot.PhasesWithSnapshots);
        Assert.Equal(snapshot.PhasesWithSnapshots.Count, phaseWaves.Sum(x => x.Count));
        Assert.All(phaseWaves, AssertWaveFitsThirtyMinuteWindow);

        var dashboard = new DashboardFeature(console, witness, functionsApi, print, eventInformation);
        await CoreAssertions.AssertArrivelistMatchesPersisted(functionsApi, witness, eventInformation.Id);
        var processedPhases = 0;
        var publishedSnapshotGroups = 0;
        foreach (var phaseWave in phaseWaves)
        {
            var result = await dashboard.SnapshotWave(phaseWave);
            processedPhases += result.ProcessedPhases;
            publishedSnapshotGroups += result.PublishedSnapshotGroups;
        }

        Assert.Equal(snapshot.PhasesWithSnapshots.Count, processedPhases);
        Assert.True(publishedSnapshotGroups > 0);

        await AssertFinalStateMatchesSnapshots(functionsApi, eventInformation, setup, snapshot);
        await print.PrintFinalRanklists(eventInformation);
        await AssertCompletedEventEndsAndCanBeExported(console, functionsApi, _fixture.Clock, eventInformation);
    }

    static async Task AssertStartedConfigureEventCannotBeUpdated(FunctionsApiDriver api, SetupConfigureEvent setupEvent)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => api.UpdateSetupConfigureEvent(setupEvent)
        );
        Assert.Contains($"Cannot mutate configure event '{setupEvent.Id}'", exception.Message);
        Assert.Contains("started", exception.Message);
    }

    static async Task SeedOtherEventData(FunctionsApiDriver api, int testedEventId)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var (historicEventId, liveEventId) = CreateOtherEventIds(testedEventId);
        var documentBase = Math.Abs(testedEventId % 1_000_000) + 1_000_000;

        await SeedOtherEvent(
            api,
            historicEventId,
            new EventSpan(today.AddDays(-30), today.AddDays(-29)),
            documentBase,
            "Historic"
        );
        await SeedOtherEvent(api, liveEventId, new EventSpan(today, today.AddDays(1)), documentBase + 10_000, "Live");
    }

    static (int Historic, int Live) CreateOtherEventIds(int testedEventId)
    {
        const int historicOffset = 10_000;
        const int liveOffset = 20_000;

        return testedEventId <= int.MaxValue - liveOffset
            ? (testedEventId + historicOffset, testedEventId + liveOffset)
            : (testedEventId - liveOffset, testedEventId - historicOffset);
    }

    static async Task SeedOtherEvent(FunctionsApiDriver api, int eventId, EventSpan eventSpan, int idBase, string label)
    {
        var eventInformation = IntegrationPayloadFactory.EventInformation(eventId, eventSpan, $"Seeded {label} Event");
        var participations = new[]
        {
            IntegrationPayloadFactory.ActiveParticipation(eventId, idBase + 1, idBase + 101),
            IntegrationPayloadFactory.ActiveParticipation(eventId, idBase + 2, idBase + 102),
        };
        var officials = new[]
        {
            IntegrationPayloadFactory.Official(eventId, userId: null, id: idBase + 201),
            IntegrationPayloadFactory.Official(eventId, userId: null, id: idBase + 202),
        };
        var operators = new[] { IntegrationPayloadFactory.Operator(eventId, userId: idBase + 250, id: idBase + 251) };
        var rankings = new[]
        {
            IntegrationPayloadFactory.Ranking(eventId, participations, idBase + 301, $"Seeded {label} Ranking A"),
            IntegrationPayloadFactory.Ranking(eventId, participations, idBase + 302, $"Seeded {label} Ranking B"),
        };
        var handouts = participations
            .Select((participation, index) => IntegrationPayloadFactory.Handout(participation, idBase + 401 + index))
            .ToArray();

        await api.Create(eventInformation);
        foreach (var participation in participations)
        {
            await api.Create(participation);
        }
        foreach (var official in officials)
        {
            await api.Create(official);
        }
        foreach (var @operator in operators)
        {
            await api.Create(@operator);
        }
        foreach (var ranking in rankings)
        {
            await api.Create(ranking);
        }
        foreach (var handout in handouts)
        {
            await api.Create(handout);
        }
    }

    static async Task<StartedEventDocuments> ReadStartedDocumentsScopedToCurrentEvent(
        FunctionsApiDriver api,
        EventInformation eventInformation,
        SetupFeatureResult setup,
        EndToEndEventSnapshot snapshot
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        StartedEventDocuments last = new([], [], [], [], []);
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = new StartedEventDocuments(
                await api.ReadParticipations(eventInformation.Id),
                await api.ReadRankings(eventInformation.Id),
                await api.ReadOfficials(eventInformation.Id),
                await api.ReadOperators(eventInformation.Id),
                await api.ReadHandouts(eventInformation.Id)
            );
            AssertDocumentsBelongToEvent(eventInformation.Id, last);

            if (
                last.Participations.Count == snapshot.Participations.Count
                && last.Rankings.Count == snapshot.Rankings.Count
                && last.Officials.Count == setup.SetupEvent.Officials.Count
                && last.Operators.Count == setup.SetupEvent.Operators.Count
                && last.Handouts.Count == 0
            )
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            "The Functions API did not reach the expected started event document counts. "
                + $"Participations: {last.Participations.Count}/{snapshot.Participations.Count}, "
                + $"Rankings: {last.Rankings.Count}/{snapshot.Rankings.Count}, "
                + $"Officials: {last.Officials.Count}/{setup.SetupEvent.Officials.Count}, "
                + $"Operators: {last.Operators.Count}/{setup.SetupEvent.Operators.Count}, "
                + $"Handouts: {last.Handouts.Count}/0."
        );
    }

    static void AssertDocumentsBelongToEvent(int eventId, StartedEventDocuments documents)
    {
        Assert.All(documents.Participations, participation => Assert.Equal(eventId, participation.EventId));
        Assert.All(documents.Officials, official => Assert.Equal(eventId, official.EventId));
        Assert.All(documents.Operators, @operator => Assert.Equal(eventId, @operator.EventId));
        Assert.All(
            documents.Rankings,
            ranking =>
            {
                Assert.Equal(eventId, ranking.EventId);
                Assert.All(ranking.Entries, entry => Assert.Equal(eventId, entry.Participation.EventId));
            }
        );
        Assert.All(
            documents.Handouts,
            handout =>
            {
                Assert.Equal(eventId, handout.EventId);
                Assert.All(handout.Entries, entry => Assert.Equal(eventId, entry.Participation.EventId));
            }
        );
    }

    static void AssertStartedOperatorsMatchSetup(
        IReadOnlyList<Operator> activeOperators,
        SetupConfigureEvent setupEvent,
        int eventId
    )
    {
        Assert.Equal(setupEvent.Operators.Count, activeOperators.Count);
        foreach (var setupOperator in setupEvent.Operators)
        {
            var activeOperator = Assert.Single(activeOperators, x => x.UserId == setupOperator.User.Id);
            Assert.Equal(eventId, activeOperator.EventId);
            Assert.Equal(setupOperator.Role, activeOperator.Role);
        }
    }

    static IReadOnlyList<IReadOnlyList<EndToEndPhaseSnapshot>> CreatePhaseWaves(
        IReadOnlyList<EndToEndPhaseSnapshot> phases
    )
    {
        return GroupByDelta(
            phases.OrderBy(x => x.ArriveTime).ThenBy(x => x.Number),
            x => x.ArriveTime!.Value,
            TimeSpan.FromMinutes(30)
        );
    }

    static void AssertWaveFitsThirtyMinuteWindow(IReadOnlyList<EndToEndPhaseSnapshot> wave)
    {
        var times = wave.Select(x => x.ArriveTime!.Value).ToArray();
        Assert.True(times.Max() - times.Min() <= TimeSpan.FromMinutes(30));
    }

    static IReadOnlyList<IReadOnlyList<EndToEndPhaseSnapshot>> GroupByDelta(
        IEnumerable<EndToEndPhaseSnapshot> source,
        Func<EndToEndPhaseSnapshot, DateTimeOffset> timestamp,
        TimeSpan delta
    )
    {
        var groups = new List<IReadOnlyList<EndToEndPhaseSnapshot>>();
        var current = new List<EndToEndPhaseSnapshot>();
        DateTimeOffset? groupStart = null;

        foreach (var item in source)
        {
            var value = timestamp(item);
            if (groupStart != null && value - groupStart.Value > delta)
            {
                groups.Add(current.ToArray());
                current = [];
                groupStart = null;
            }

            groupStart ??= value;
            current.Add(item);
        }

        if (current.Count != 0)
        {
            groups.Add(current.ToArray());
        }

        return groups;
    }

    static async Task AssertFinalStateMatchesSnapshots(
        FunctionsApiDriver api,
        EventInformation eventInformation,
        SetupFeatureResult setup,
        EndToEndEventSnapshot snapshot
    )
    {
        var participations = await api.ReadParticipations(eventInformation.Id);
        var rankings = await api.ReadRankings(eventInformation.Id);
        var idMap = new Dictionary<int, int>(setup.IdMap) { [snapshot.EventId] = eventInformation.Id };
        foreach (var mapping in snapshot.CreateIdMap(participations, rankings))
        {
            idMap[mapping.Key] = mapping.Value;
        }

        var expectedParticipations = snapshot.ExpectedParticipationsWith(idMap);
        var actualParticipations = SnapshotJson.Canonicalize(
            participations.OrderBy(x => x.Combination.Number).Select(ParticipationModel.MapFrom).ToArray()
        );
        Assert.Equal(expectedParticipations.ToString(Formatting.None), actualParticipations.ToString(Formatting.None));

        var expectedRankings = snapshot.ExpectedRankingsWith(idMap);
        var actualRankings = SnapshotJson.Canonicalize(
            rankings.OrderBy(x => x.Name).ThenBy(x => x.Category).Select(RankingModel.From).ToArray()
        );
        Assert.Equal(expectedRankings.ToString(Formatting.None), actualRankings.ToString(Formatting.None));
    }

    static async Task AssertCompletedEventEndsAndCanBeExported(
        ConsoleDriver console,
        FunctionsApiDriver api,
        OffsetTimeProvider clock,
        EventInformation eventInformation
    )
    {
        eventInformation = CreateFeiExportEventInformation(eventInformation);
        await api.Update(eventInformation);
        var finalRankings = await api.ReadRankings(eventInformation.Id);
        var exportableRanking = CreateFeiExportRanking(finalRankings.First());
        await api.Update(exportableRanking);

        // An Event is Live until the end of its last day and Historic from then on: it is not ended by an action, the
        // clock of the Api moves past the end (ADR-0007).
        Assert.True(console.IsConnected);
        var events = console.GetRequiredService<IEventInformationService>();
        Assert.Contains(await events.GetLive(), x => x.Id == eventInformation.Id);
        clock.Advance(eventInformation.EventSpan.EndDay - clock.GetUtcNow() + TimeSpan.FromSeconds(1));

        var liveEvents = await events.GetLive();
        Assert.DoesNotContain(liveEvents, x => x.Id == eventInformation.Id);
        var historicEvents = await events.GetHistoric();
        Assert.Contains(historicEvents, x => x.Id == eventInformation.Id);

        var historicEventsService = console.GetRequiredService<IHistoricEventService>();
        await historicEventsService.LoadEvent(eventInformation.Id);

        var historicEvent = Assert.IsType<EventInformation>(historicEventsService.Event);
        var export = console
            .GetRequiredService<IFeiExportService>()
            .Create(historicEvent, historicEventsService.Rankings);

        Assert.Equal("application/xml", export.ContentType);
        Assert.Contains(eventInformation.FeiShowId!, export.Content);
        Assert.Contains(exportableRanking.FeiEventId!, export.Content);
        Assert.Contains(exportableRanking.FeiCompetitionId!, export.Content);
    }

    static EventInformation CreateFeiExportEventInformation(EventInformation source)
    {
        return new EventInformation(
            source.Country,
            source.Name,
            source.Location,
            source.EventSpan,
            $"FEI-SHOW-{source.Id}",
            source.Id
        );
    }

    static Ranking CreateFeiExportRanking(Ranking source)
    {
        var entries = source
            .Entries.Select(
                (entry, index) =>
                    new RankingEntry(
                        CreateFeiExportParticipation(entry.Participation),
                        entry.Rank,
                        entry.IsNotRanked,
                        entry.Id
                    )
            )
            .ToList();

        return new Ranking(
            source.Name,
            source.Ruleset,
            source.Category,
            $"FEI-EVENT-{source.Id}",
            "CEI1",
            $"FEI-COMPETITION-{source.Id}",
            "E Comp",
            "01",
            entries,
            source.EventId,
            source.Id
        );
    }

    static Participation CreateFeiExportParticipation(Participation source)
    {
        var athlete = source.Combination.Athlete;
        var horse = source.Combination.Horse;
        var athleteFeiId = (100000 + source.Combination.Number).ToString();
        var horseFeiId = (200000 + source.Combination.Number).ToString();
        var exportAthlete = new CoreAthlete(
            athlete.Name,
            athlete.NameEnglish,
            athlete.Country,
            athlete.Club,
            athleteFeiId,
            athlete.Id
        );
        var exportHorse = new CoreHorse(horse.Name, horse.NameEnglish, horseFeiId, horse.Id);
        var exportCombination = new CoreCombination(
            source.Combination.Number,
            exportAthlete,
            exportHorse,
            source.Combination.Club,
            source.Combination.Distance,
            source.Combination.MinAverageSpeed,
            source.Combination.MaxAverageSpeed,
            source.Combination.Id
        );

        return new Participation(
            source.Category,
            source.Competition,
            exportCombination,
            source.Phases,
            source.Eliminated,
            source.EventId,
            source.Id
        );
    }

    sealed class StartedEventDocuments
    {
        public StartedEventDocuments(
            IReadOnlyList<Participation> participations,
            IReadOnlyList<Ranking> rankings,
            IReadOnlyList<Official> officials,
            IReadOnlyList<Operator> operators,
            IReadOnlyList<Handout> handouts
        )
        {
            Participations = participations;
            Rankings = rankings;
            Officials = officials;
            Operators = operators;
            Handouts = handouts;
        }

        public IReadOnlyList<Participation> Participations { get; }
        public IReadOnlyList<Ranking> Rankings { get; }
        public IReadOnlyList<Official> Officials { get; }
        public IReadOnlyList<Operator> Operators { get; }
        public IReadOnlyList<Handout> Handouts { get; }
    }
}
