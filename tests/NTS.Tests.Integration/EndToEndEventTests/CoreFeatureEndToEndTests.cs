using Newtonsoft.Json;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.EndToEndEventTests.Features;
using NTS.Tests.Integration.EndToEndEventTests.Helpers;
using NTS.Tests.Integration.Infrastructure;

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

        var setup = await configureEvent.Execute(snapshot);
        var enduranceEvent = await startEvent.Execute(setup);

        var startedParticipations = await Eventually.ReadParticipations(
            nexusApi,
            enduranceEvent.Id,
            items => items.Count == snapshot.Participations.Count,
            "started participations"
        );
        var startedRankings = await Eventually.ReadRankings(
            nexusApi,
            enduranceEvent.Id,
            items => items.Count == snapshot.Rankings.Count,
            "started rankings"
        );
        Assert.Equal(snapshot.Participations.Count, startedParticipations.Count);
        Assert.Equal(snapshot.Rankings.Count, startedRankings.Count);

        await using var witness = new WitnessDriver(
            _fixture.WarpBaseUrl,
            _fixture.NexusBaseUrl,
            setup.WitnessOfficial,
            $"CoreEndToEndWitness-{snapshot.Name}"
        );
        await witness.Start();
        await witness.Connect(enduranceEvent);

        var phaseWaves = CreatePhaseWaves(snapshot.PhasesWithSnapshots);
        Assert.Equal(snapshot.PhasesWithSnapshots.Count, phaseWaves.Sum(x => x.Count));
        Assert.All(phaseWaves, AssertWaveFitsThirtyMinuteWindow);

        var dashboard = new DashboardFeature(
            judge,
            witness,
            nexusApi,
            enduranceEvent
        );
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

        await AssertFinalStateMatchesSnapshots(nexusApi, enduranceEvent, setup, snapshot);
    }

    static IReadOnlyList<IReadOnlyList<EndToEndPhaseSnapshot>> CreatePhaseWaves(
        IReadOnlyList<EndToEndPhaseSnapshot> phases
    )
    {
        return GroupByDelta(phases.OrderBy(x => x.ArriveTime).ThenBy(x => x.Number), x => x.ArriveTime!.Value, TimeSpan.FromMinutes(30));
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
        EnduranceEvent enduranceEvent,
        SetupFeatureResult setup,
        EndToEndEventSnapshot snapshot
    )
    {
        var participations = await api.ReadParticipations(enduranceEvent.Id);
        var rankings = await api.ReadRankings(enduranceEvent.Id);
        var idMap = new Dictionary<int, int>(setup.IdMap) { [snapshot.EventId] = enduranceEvent.Id };
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
}
