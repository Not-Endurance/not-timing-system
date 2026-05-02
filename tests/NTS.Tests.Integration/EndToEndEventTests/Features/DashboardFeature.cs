using Microsoft.Extensions.DependencyInjection;
using Not.Application.Behinds.Adapters;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Startlists;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Judge.Contracts.Features.Core.Dashboard;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.EndToEndEventTests.Helpers;
using WitnessSnapshotService = NTS.Witness.Contracts.Features.Snapshots.ISnapshotService;

namespace NTS.Tests.Integration.EndToEndEventTests.Features;

internal sealed class DashboardFeature
{
    const string FEATURE = "Dashboard";
    static readonly TimeSpan ARRIVE_SNAPSHOT_GROUP_DELTA = TimeSpan.FromMinutes(1);
    static readonly TimeSpan PRESENT_SNAPSHOT_GROUP_DELTA = TimeSpan.FromSeconds(10);

    readonly JudgeDriver _judge;
    readonly WitnessDriver _witness;
    readonly NexusApiDriver _api;
    readonly EnduranceEvent _enduranceEvent;
    readonly HashSet<int> _manuallyEliminated = [];
    int _innerWaveNumber;
    int _outerWaveNumber;

    public DashboardFeature(
        JudgeDriver judge,
        WitnessDriver witness,
        NexusApiDriver api,
        EnduranceEvent enduranceEvent
    )
    {
        _judge = judge;
        _witness = witness;
        _api = api;
        _enduranceEvent = enduranceEvent;
    }

    public async Task<DashboardFeatureResult> SnapshotWave(IReadOnlyList<EndToEndPhaseSnapshot> wave)
    {
        var outerWaveNumber = ++_outerWaveNumber;
        var publishedGroups = 0;

        foreach (var group in GroupByDelta(wave, x => x.ArriveTime!.Value, ARRIVE_SNAPSHOT_GROUP_DELTA))
        {
            publishedGroups += await FeatureStep.Run(
                FEATURE,
                GroupStep(outerWaveNumber, "publish arrival snapshot group"),
                group,
                () => PublishSnapshotGroup(group, SnapshotType.Arrive, x => x.ArriveTime!.Value)
            );

            foreach (var entry in group)
            {
                await FeatureStep.Run(
                    FEATURE,
                    $"Wave {outerWaveNumber}: assert arrival persisted",
                    entry,
                    () => AssertArriveSnapshot(entry)
                );
            }

            await FeatureStep.Run(
                FEATURE,
                $"Wave {outerWaveNumber}: assert startlists after arrival group",
                group,
                () => CoreAssertions.AssertStartlistsMatchPersisted(_api, _judge, _witness, _enduranceEvent.Id)
            );
        }

        var presentEntries = wave.Where(x => x.PresentTime != null).ToArray();
        foreach (var group in GroupByDelta(presentEntries, x => x.PresentTime!.Value, PRESENT_SNAPSHOT_GROUP_DELTA))
        {
            publishedGroups += await FeatureStep.Run(
                FEATURE,
                GroupStep(outerWaveNumber, "publish presentation snapshot group"),
                group,
                () => PublishSnapshotGroup(group, SnapshotType.Present, x => x.PresentTime!.Value)
            );

            foreach (var entry in group)
            {
                await FeatureStep.Run(
                    FEATURE,
                    $"Wave {outerWaveNumber}: assert presentation persisted",
                    entry,
                    () => AssertPresentSnapshot(entry)
                );
            }

            foreach (var entry in group)
            {
                await FeatureStep.Run(
                    FEATURE,
                    $"Wave {outerWaveNumber}: apply requested inspection",
                    entry,
                    () => ApplyRequestedInspection(entry)
                );
                await FeatureStep.Run(
                    FEATURE,
                    $"Wave {outerWaveNumber}: apply requested representation",
                    entry,
                    () => ApplyRequestedRepresentation(entry)
                );
            }

            var representEntries = group.Where(x => x.RepresentTime != null).ToArray();
            foreach (var representGroup in GroupByDelta(
                representEntries,
                x => x.RepresentTime!.Value,
                PRESENT_SNAPSHOT_GROUP_DELTA
            ))
            {
                publishedGroups += await FeatureStep.Run(
                    FEATURE,
                    GroupStep(outerWaveNumber, "publish representation snapshot group"),
                    representGroup,
                    () => PublishSnapshotGroup(
                        representGroup,
                        SnapshotType.Present,
                        x => x.RepresentTime!.Value
                    )
                );

                foreach (var entry in representGroup)
                {
                    await FeatureStep.Run(
                        FEATURE,
                        $"Wave {outerWaveNumber}: assert representation persisted",
                        entry,
                        () => AssertRepresentSnapshot(entry)
                    );
                }
            }

            foreach (var entry in group)
            {
                await FeatureStep.Run(
                    FEATURE,
                    $"Wave {outerWaveNumber}: assert phase side effects",
                    entry,
                    () => AssertPhaseSideEffects(entry)
                );
                await FeatureStep.Run(
                    FEATURE,
                    $"Wave {outerWaveNumber}: apply manual elimination",
                    entry,
                    () => ApplyManualElimination(entry)
                );
            }

            await FeatureStep.Run(
                FEATURE,
                $"Wave {outerWaveNumber}: assert startlists after presentation group",
                group,
                () => CoreAssertions.AssertStartlistsMatchPersisted(_api, _judge, _witness, _enduranceEvent.Id)
            );
        }

        return new DashboardFeatureResult(wave.Count, publishedGroups);
    }

    string GroupStep(int outerWaveNumber, string action)
    {
        var innerWaveNumber = _innerWaveNumber + 1;
        var pending = innerWaveNumber % 2 == 0 ? " with Judge disconnected" : "";
        return $"Wave {outerWaveNumber}, inner wave {innerWaveNumber}: {action}{pending}";
    }

    async Task<int> PublishSnapshotGroup(
        IReadOnlyList<EndToEndPhaseSnapshot> group,
        SnapshotType snapshotType,
        Func<EndToEndPhaseSnapshot, DateTimeOffset> timestamp
    )
    {
        if (group.Count == 0)
        {
            return 0;
        }

        var shouldUsePendingDelivery = ++_innerWaveNumber % 2 == 0;
        if (shouldUsePendingDelivery)
        {
            await _judge.Disconnect();
        }

        try
        {
            var snapshots = _witness.GetRequiredService<WitnessSnapshotService>();
            var historyCount = snapshots.History.Count;
            foreach (var entry in group)
            {
                var participation = await WaitForSnapshotCandidate(snapshots, entry.Number);
                snapshots.SelectForSnapshot(participation);
                var snapshot = snapshots.Snapshots.Single(x => x.Number == entry.Number);
                snapshots.Capture(snapshot);
                snapshots.UpdateTimestamp(snapshot, new Timestamp(timestamp(entry)));
            }

            Assert.True(
                await snapshots.Publish(snapshotType),
                $"Witness did not publish a {snapshotType} snapshot group for #{string.Join(", #", group.Select(x => x.Number))}."
            );

            var published = snapshots.History.Skip(historyCount).Single();
            Assert.Equal(snapshotType, published.Type);
            Assert.Equal(
                group.Select(x => x.Number).OrderBy(x => x),
                published.Entries.Select(x => x.Number).OrderBy(x => x)
            );
        }
        finally
        {
            if (shouldUsePendingDelivery)
            {
                await _judge.Connect(_enduranceEvent);
            }
        }

        return 1;
    }

    async Task AssertArriveSnapshot(EndToEndPhaseSnapshot entry)
    {
        var persisted = await Eventually.ReadParticipation(
            _api,
            _enduranceEvent.Id,
            entry.Number,
            participation => CoreAssertions.SameTimeOfDay(
                participation.Phases[entry.PhaseIndex].ArriveTime,
                entry.ArriveTime!.Value
            ),
            $"arrival for #{entry.Number} phase {entry.PhaseNumber}"
        );
        CoreAssertions.AssertEqualTimeOfDay(entry.ArriveTime!.Value, persisted.Phases[entry.PhaseIndex].ArriveTime);

        if (entry.PresentTime == null && entry.RepresentTime == null)
        {
            CoreAssertions.AssertPhaseMatches(entry.Phase, persisted.Phases[entry.PhaseIndex]);
        }

    }

    async Task AssertPresentSnapshot(EndToEndPhaseSnapshot entry)
    {
        var persisted = await Eventually.ReadParticipation(
            _api,
            _enduranceEvent.Id,
            entry.Number,
            participation => CoreAssertions.SameTimeOfDay(
                participation.Phases[entry.PhaseIndex].PresentTime,
                entry.PresentTime!.Value
            ),
            $"presentation for #{entry.Number} phase {entry.PhaseNumber}"
        );
        CoreAssertions.AssertEqualTimeOfDay(entry.PresentTime!.Value, persisted.Phases[entry.PhaseIndex].PresentTime);

    }

    async Task AssertRepresentSnapshot(EndToEndPhaseSnapshot entry)
    {
        var persisted = await Eventually.ReadParticipation(
            _api,
            _enduranceEvent.Id,
            entry.Number,
            participation => CoreAssertions.SameTimeOfDay(
                participation.Phases[entry.PhaseIndex].RepresentTime,
                entry.RepresentTime!.Value
            ),
            $"representation for #{entry.Number} phase {entry.PhaseNumber}"
        );
        CoreAssertions.AssertEqualTimeOfDay(entry.RepresentTime!.Value, persisted.Phases[entry.PhaseIndex].RepresentTime);

    }

    async Task ApplyRequestedInspection(EndToEndPhaseSnapshot entry)
    {
        if (!entry.Phase.IsRequiredInspectionRequested || entry.Phase.IsRequiredInspectionCompulsory)
        {
            return;
        }

        await SelectParticipation(entry.Number);
        await _judge.GetRequiredService<IInspectionService>().RequestInspection(true);
        await Eventually.ReadParticipation(
            _api,
            _enduranceEvent.Id,
            entry.Number,
            participation => participation.Phases[entry.PhaseIndex].IsRequiredInspectionRequested,
            $"requested inspection for #{entry.Number} phase {entry.PhaseNumber}"
        );
    }

    async Task ApplyRequestedRepresentation(EndToEndPhaseSnapshot entry)
    {
        if (!entry.Phase.IsReinspectionRequested)
        {
            return;
        }

        await SelectParticipation(entry.Number);
        await _judge.GetRequiredService<IInspectionService>().RequestRepresent(true);
        await Eventually.ReadParticipation(
            _api,
            _enduranceEvent.Id,
            entry.Number,
            participation => participation.Phases[entry.PhaseIndex].IsReinspectionRequested,
            $"requested representation for #{entry.Number} phase {entry.PhaseNumber}"
        );
    }

    async Task AssertPhaseSideEffects(EndToEndPhaseSnapshot entry)
    {
        var persisted = await Eventually.ReadParticipation(
            _api,
            _enduranceEvent.Id,
            entry.Number,
            participation => PhaseReachedExpectedState(participation, entry),
            $"expected phase state for #{entry.Number} phase {entry.PhaseNumber}"
        );
        CoreAssertions.AssertPhaseMatches(entry.Phase, persisted.Phases[entry.PhaseIndex]);

        if (!entry.Phase.IsComplete())
        {
            return;
        }

        if (entry.ExpectedEliminated != null && IsAutomaticElimination(entry.ExpectedEliminated))
        {
            await AssertEliminationSideEffects(entry.Number, entry.ExpectedEliminated);
            return;
        }

        await Eventually.ReadHandouts(
            _api,
            _enduranceEvent.Id,
            items => items.Any(x => x.Participation.Combination.Number == entry.Number),
            $"handout for #{entry.Number}"
        );
        await Eventually.ReadRankings(
            _api,
            _enduranceEvent.Id,
            rankings => rankings.SelectMany(x => x.Entries).Any(x =>
                x.Participation.Combination.Number == entry.Number
                && x.Participation.Phases[entry.PhaseIndex].IsComplete()
            ),
            $"ranking update for #{entry.Number} phase {entry.PhaseNumber}"
        );
        await _witness.WaitForParticipation(
            entry.Number,
            participation => participation.Phases[entry.PhaseIndex].IsComplete(),
            TimeSpan.FromSeconds(10)
        );
        await CoreAssertions.AssertStartlistsMatchPersisted(_api, _judge, _witness, _enduranceEvent.Id);
    }

    async Task ApplyManualElimination(EndToEndPhaseSnapshot entry)
    {
        var expected = entry.ExpectedEliminated;
        if (expected == null || IsAutomaticElimination(expected) || !_manuallyEliminated.Add(entry.Number))
        {
            return;
        }

        await SelectParticipation(entry.Number);
        var eliminations = _judge.GetRequiredService<IEliminationService>();
        switch (expected)
        {
            case Withdrawn:
                await eliminations.Withdraw();
                break;
            case Retired:
                await eliminations.Retire();
                break;
            case FinishedNotRanked finishedNotRanked:
                await eliminations.FinishNotRanked(finishedNotRanked.Complement ?? "Snapshot");
                break;
            case Disqualified disqualified:
                await eliminations.Disqualify(disqualified.DqCodes.ToArray(), disqualified.Complement);
                break;
            case FailedToQualify failedToQualify:
                await eliminations.FailToQualify(failedToQualify.FtqCodes.ToArray(), failedToQualify.Complement);
                break;
            default:
                throw new InvalidOperationException($"Unknown elimination type '{expected.GetType().Name}'.");
        }

        await AssertEliminationSideEffects(entry.Number, expected);
    }

    static bool PhaseReachedExpectedState(Participation participation, EndToEndPhaseSnapshot entry)
    {
        var phase = participation.Phases[entry.PhaseIndex];
        return CoreAssertions.SameTimeOfDay(phase.ArriveTime, entry.ArriveTime!.Value)
            && (entry.PresentTime == null || CoreAssertions.SameTimeOfDay(phase.PresentTime, entry.PresentTime.Value))
            && (
                entry.RepresentTime == null
                || CoreAssertions.SameTimeOfDay(phase.RepresentTime, entry.RepresentTime.Value)
            )
            && phase.IsReinspectionRequested == entry.Phase.IsReinspectionRequested
            && phase.IsRequiredInspectionRequested == entry.Phase.IsRequiredInspectionRequested
            && phase.IsRequiredInspectionCompulsory == entry.Phase.IsRequiredInspectionCompulsory;
    }

    async Task AssertEliminationSideEffects(
        int number,
        Eliminated expected
    )
    {
        await Eventually.ReadParticipation(
            _api,
            _enduranceEvent.Id,
            number,
            participation => participation.Eliminated?.ToString() == expected.ToString(),
            $"elimination for #{number}"
        );
        await Eventually.ReadRankings(
            _api,
            _enduranceEvent.Id,
            rankings => rankings.SelectMany(x => x.Entries).Any(x =>
                x.Participation.Combination.Number == number
                && x.Participation.Eliminated?.ToString() == expected.ToString()
            ),
            $"ranking elimination for #{number}"
        );
        await WaitForWitnessAbsence(number);
    }

    static bool IsAutomaticElimination(Eliminated eliminated)
    {
        return eliminated is FailedToQualify failedToQualify
            && failedToQualify.FtqCodes.All(x => x is FailToQualifyCode.SP or FailToQualifyCode.OT);
    }

    async Task SelectParticipation(int number)
    {
        var context = _judge.GetRequiredService<IParticipationContext>();
        if (context is NStatefulService stateful)
        {
            stateful.ResetHasLoaded();
        }

        await context.Load();
        context.Selected = context.Participations.Single(x => x.Combination.Number == number);
    }

    static async Task<Participation> WaitForSnapshotCandidate(WitnessSnapshotService snapshots, int number)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await snapshots.Load();
            var participation = snapshots.Participations.FirstOrDefault(x => x.Combination.Number == number);
            if (participation != null)
            {
                return participation;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Witness snapshot list did not include participation #{number}.");
    }

    async Task WaitForWitnessAbsence(int number)
    {
        var context = _witness.GetRequiredService<IParticipationContext>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (context.Participations.All(x => x.Combination.Number != number))
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Witness still listed eliminated participation #{number}.");
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

        foreach (var item in source.OrderBy(timestamp).ThenBy(x => x.Number).ThenBy(x => x.PhaseIndex))
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
}

internal sealed class DashboardFeatureResult
{
    public DashboardFeatureResult(int processedPhases, int publishedSnapshotGroups)
    {
        ProcessedPhases = processedPhases;
        PublishedSnapshotGroups = publishedSnapshotGroups;
    }

    public int ProcessedPhases { get; }
    public int PublishedSnapshotGroups { get; }
}
