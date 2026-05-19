using Newtonsoft.Json;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.PastEvents;
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
        await using var judge = new JudgeDriver(_fixture.WarpBaseUrl, _fixture.NexusBaseUrl);
        using var nexusApi = new NexusApiDriver(_fixture.NexusBaseUrl);
        var configureEvent = new ConfigureEventFeature(judge, nexusApi);
        var startEvent = new StartCoreEventFeature(judge, nexusApi);

        await SeedOtherEventData(nexusApi, snapshot.EventId);

        var setup = await configureEvent.Execute(snapshot);
        var eventInformation = await startEvent.Execute(setup);
        await AssertStartedConfigureEventCannotBeUpdated(nexusApi, setup.SetupEvent);

        var startedDocuments = await ReadStartedDocumentsScopedToCurrentEvent(
            nexusApi,
            eventInformation,
            setup,
            snapshot
        );
        var startedParticipations = startedDocuments.Participations;
        var startedRankings = startedDocuments.Rankings;
        Assert.Equal(snapshot.Participations.Count, startedParticipations.Count);
        Assert.Equal(snapshot.Rankings.Count, startedRankings.Count);

        await using var witness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            setup.WitnessOfficial,
            $"CoreEndToEndWitness-{snapshot.Name}"
        );
        await witness.Start();
        await witness.Connect(eventInformation);

        var phaseWaves = CreatePhaseWaves(snapshot.PhasesWithSnapshots);
        Assert.Equal(snapshot.PhasesWithSnapshots.Count, phaseWaves.Sum(x => x.Count));
        Assert.All(phaseWaves, AssertWaveFitsThirtyMinuteWindow);

        var dashboard = new DashboardFeature(judge, witness, nexusApi, eventInformation);
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

        await AssertFinalStateMatchesSnapshots(nexusApi, eventInformation, setup, snapshot);
        await AssertCompletedEventCanBeDeactivatedAndExported(judge, nexusApi, eventInformation);
    }

    static async Task AssertStartedConfigureEventCannotBeUpdated(NexusApiDriver api, SetupConfigureEvent setupEvent)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => api.UpdateSetupConfigureEvent(setupEvent)
        );
        Assert.Contains($"Cannot mutate configure event '{setupEvent.Id}'", exception.Message);
        Assert.Contains("started", exception.Message);
    }

    static async Task SeedOtherEventData(NexusApiDriver api, int testedEventId)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var (pastEventId, activeEventId) = CreateOtherEventIds(testedEventId);
        var documentBase = Math.Abs(testedEventId % 1_000_000) + 1_000_000;

        await SeedOtherEvent(
            api,
            pastEventId,
            new EventSpan(today.AddDays(-30), today.AddDays(-29)),
            documentBase,
            "Past"
        );
        await SeedOtherEvent(
            api,
            activeEventId,
            new EventSpan(today, today.AddDays(1)),
            documentBase + 10_000,
            "Active"
        );
    }

    static (int Past, int Active) CreateOtherEventIds(int testedEventId)
    {
        const int pastOffset = 10_000;
        const int activeOffset = 20_000;

        return testedEventId <= int.MaxValue - activeOffset
            ? (testedEventId + pastOffset, testedEventId + activeOffset)
            : (testedEventId - activeOffset, testedEventId - pastOffset);
    }

    static async Task SeedOtherEvent(NexusApiDriver api, int eventId, EventSpan eventSpan, int idBase, string label)
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
        NexusApiDriver api,
        EventInformation eventInformation,
        SetupFeatureResult setup,
        EndToEndEventSnapshot snapshot
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        StartedEventDocuments last = new([], [], [], []);
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = new StartedEventDocuments(
                await api.ReadParticipations(eventInformation.Id),
                await api.ReadRankings(eventInformation.Id),
                await api.ReadOfficials(eventInformation.Id),
                await api.ReadHandouts(eventInformation.Id)
            );
            AssertDocumentsBelongToEvent(eventInformation.Id, last);

            if (
                last.Participations.Count == snapshot.Participations.Count
                && last.Rankings.Count == snapshot.Rankings.Count
                && last.Officials.Count == setup.SetupEvent.Officials.Count
                && last.Handouts.Count == 0
            )
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            "Nexus API did not reach the expected started event document counts. "
                + $"Participations: {last.Participations.Count}/{snapshot.Participations.Count}, "
                + $"Rankings: {last.Rankings.Count}/{snapshot.Rankings.Count}, "
                + $"Officials: {last.Officials.Count}/{setup.SetupEvent.Officials.Count}, "
                + $"Handouts: {last.Handouts.Count}/0."
        );
    }

    static void AssertDocumentsBelongToEvent(int eventId, StartedEventDocuments documents)
    {
        Assert.All(documents.Participations, participation => Assert.Equal(eventId, participation.EventId));
        Assert.All(documents.Officials, official => Assert.Equal(eventId, official.EventId));
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
                Assert.Equal(eventId, handout.Participation.EventId);
            }
        );
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
        NexusApiDriver api,
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

    static async Task AssertCompletedEventCanBeDeactivatedAndExported(
        JudgeDriver judge,
        NexusApiDriver api,
        EventInformation eventInformation
    )
    {
        eventInformation = CreateFeiExportEventInformation(eventInformation);
        await api.Update(eventInformation);
        var finalRankings = await api.ReadRankings(eventInformation.Id);
        var exportableRanking = CreateFeiExportRanking(finalRankings.First());
        await api.Update(exportableRanking);

        Assert.True(judge.IsConnected);
        await judge.GetRequiredService<IDashService>().Deactivate();

        Assert.False(judge.IsConnected);
        var activeEvents = await api.ReadActiveEventInformation();
        Assert.DoesNotContain(activeEvents, x => x.Id == eventInformation.Id);
        var pastEvents = await api.ReadPastEventInformation();
        Assert.Contains(pastEvents, x => x.Id == eventInformation.Id);

        var pastEventsService = judge.GetRequiredService<IPastEventService>();
        await pastEventsService.LoadEvent(eventInformation.Id);

        var pastEvent = Assert.IsType<EventInformation>(pastEventsService.Event);
        var export = judge.GetRequiredService<IFeiExportService>().Create(pastEvent, pastEventsService.Rankings);

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
            source.Id,
            source.IsActive
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
            source.Type,
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
        var exportAthlete = new CoreAthlete(athlete.Names, athlete.Country, athlete.Club, athleteFeiId, athlete.Id);
        var exportHorse = new CoreHorse(horse.Name, horseFeiId, horse.Id);
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
            IReadOnlyList<Handout> handouts
        )
        {
            Participations = participations;
            Rankings = rankings;
            Officials = officials;
            Handouts = handouts;
        }

        public IReadOnlyList<Participation> Participations { get; }
        public IReadOnlyList<Ranking> Rankings { get; }
        public IReadOnlyList<Official> Officials { get; }
        public IReadOnlyList<Handout> Handouts { get; }
    }
}
