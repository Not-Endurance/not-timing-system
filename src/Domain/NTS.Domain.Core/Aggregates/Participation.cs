using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using static NTS.Domain.Core.Aggregates.SnapshotResultType;

namespace NTS.Domain.Core.Aggregates;

public class Participation : Aggregate, IEventScoped
{
    static readonly FailedToQualify OUT_OF_TIME = new([FailToQualifyCode.OT]);
    static readonly FailedToQualify SPEED_RESTRICTION = new([FailToQualifyCode.SP]);

    public Participation(
        ParticipationCategory category,
        Competition competition,
        Combination combination,
        PhaseCollection phases,
        Eliminated? notQualified,
        Guid eventId,
        Guid? id = null
    )
        : base(id)
    {
        EventId = eventId;
        Category = category;
        Competition = competition;
        Combination = combination;
        Phases = phases;
        Eliminated = notQualified;
    }

    public Guid EventId { get; }
    public Competition Competition { get; }
    public Combination Combination { get; }
    public ParticipationCategory Category { get; }
    public PhaseCollection Phases { get; }
    public Eliminated? Eliminated { get; private set; }

    public bool IsEliminated()
    {
        return Eliminated != null;
    }

    public bool IsComplete()
    {
        return !IsEliminated() && Phases.All(x => x.IsComplete());
    }

    public Total? GetTotal()
    {
        if (Phases.All(x => !x.IsComplete()))
        {
            return null;
        }
        return new Total(Phases);
    }

    public override string ToString()
    {
        return Combine(Combination.GetDisplayName(Competition.Ruleset), Phases, Eliminated);
    }

    //TODO rename to smthing better (including ISnapshotProcessor, IManualProcessor and other mentions..)
    public SnapshotResult Process(Snapshot snapshot)
    {
        var result = Phases.Process(snapshot, EventId);
        if (result.Type == SnapshotResultType.ActivePhaseComplete)
        {
            if (!Phases.SelectNext())
            {
                return SnapshotResult.NotApplied(EventId, snapshot, NotAppliedDueToParticipationComplete);
            }

            result = Phases.Process(snapshot, EventId);
        }
        if (Eliminated == null && result.Type == Applied)
        {
            EvaluatePhase(Phases.Current);
        }
        return result;
    }

    public void Update(IPhaseState state)
    {
        var phase = Phases.FirstOrDefault(x => x.Id == state.Id);
        GuardHelper.ThrowIfDefault(phase);

        phase.Update(state);
        EvaluatePhase(phase);
    }

    public void ToggleRepresentation(bool isRequested)
    {
        if (isRequested)
        {
            Phases.Current.RequireRepresentation();
        }
        else
        {
            Phases.Current.DisableRepresentation();
        }
    }

    public void ToggleInspection(bool isRequested)
    {
        if (isRequested)
        {
            Phases.Current.RequestInspection();
        }
        else
        {
            // TODO: rename to IsInspectionRequested
            Phases.Current.IsRequiredInspectionRequested = false;
        }
    }

    public void Withdraw()
    {
        Eliminate(new Withdrawn());
    }

    public void Retire()
    {
        Eliminate(new Retired());
    }

    public void Disqualify(DisqualifyCode[] codes, string? reason)
    {
        Eliminate(new Disqualified(codes, reason));
    }

    public void FinishNotRanked(string reason)
    {
        Eliminate(new FinishedNotRanked(reason));
    }

    public void FailToQualify(FailToQualifyCode[] codes, string? reason)
    {
        Eliminate(new FailedToQualify(codes, reason));
    }

    public void Restore()
    {
        Eliminated = null;
        if (!Phases.Current.IsComplete())
        {
            return;
        }

        var completed = Phases.Current;
        Phases.StartIfNext();
        Raise(Completed(completed));
    }

    void EvaluatePhase(Phase phase)
    {
        if (phase.ViolatesRecoveryTime())
        {
            Eliminate(OUT_OF_TIME);
            return;
        }
        if (Eliminated == OUT_OF_TIME || Eliminated == SPEED_RESTRICTION)
        {
            Restore();
        }
        if (!phase.IsComplete())
        {
            return;
        }
        if (!ReferenceEquals(phase, Phases.Current))
        {
            return;
        }

        Phases.StartIfNext();
        Raise(Completed(phase));
    }

    PhaseCompleted Completed(Phase phase)
    {
        return new PhaseCompleted(Id, Combination.Number, phase.Id, phase.IsFinal);
    }

    void Eliminate(Eliminated notQualified)
    {
        Eliminated = notQualified;
    }
}
