using System.Collections.ObjectModel;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Extensions;

namespace NTS.Domain.Core.Aggregates.Participations.Objects;

public class PhaseCollection : ReadOnlyCollection<Phase>
{
    public PhaseCollection(IEnumerable<Phase> phases)
        : base(phases.ToList())
    {
        var distanceSoFar = 0d;
        foreach (var phase in phases)
        {
            distanceSoFar += phase.Length;
            var number = phases.NumberOf(phase);
            phase.SetGate(number, distanceSoFar);
        }
        Current = this.LastOrDefault(x => x.IsComplete()) ?? this.First();
    }

    public Phase Current { get; private set; }
    public double Distance => this.Sum(x => x.Length);

    /// <summary>The Phase a time belongs to, by the rule and with the limits that <see cref="Participation.PhaseAt"/> describes.</summary>
    internal Phase PhaseAt(Timestamp time)
    {
        if (IsLast())
        {
            return Current;
        }

        var next = GetNext();
        return next.StartTime != null && time >= next.StartTime ? next : Current;
    }

    /// <summary>Makes the Phase the current one: the Participation is in it once a Snapshot or a request was placed there.</summary>
    internal void Select(Phase phase)
    {
        Current = phase;
    }

    /// <summary>
    /// Places the Snapshot in a Phase once and applies it there. A complete final Phase takes nothing more. The Phase
    /// that applied it is the current one afterwards.
    /// </summary>
    internal SnapshotResult Process(Snapshot snapshot, Guid eventId)
    {
        var phase = PhaseAt(snapshot.Timestamp);
        if (phase.IsFinal && phase.IsComplete())
        {
            return SnapshotResult.NotApplied(
                eventId,
                snapshot,
                SnapshotResultType.NotAppliedDueToParticipationComplete
            );
        }

        var result = phase.Process(snapshot, eventId);
        if (result.Type == SnapshotResultType.Applied)
        {
            Select(phase);
        }

        return result;
    }

    internal void StartIfNext()
    {
        if (!Current.IsComplete())
        {
            throw GuardHelper.Exception("Cannot start next phase while current is active");
        }
        if (IsLast())
        {
            return;
        }
        var next = GetNext();
        next.StartTime = Current.GetOutTime();
    }

    public override string ToString()
    {
        var completed = this.Count(x => x.IsComplete());
        return $"{Distance.RoundNumberToTens()}{km_string}: {completed}/{Count}";
    }

    public Phase GetNext()
    {
        var currentIndex = IndexOf(Current);
        return this[++currentIndex];
    }

    bool IsLast()
    {
        return IndexOf(Current) == Count - 1;
    }
}
