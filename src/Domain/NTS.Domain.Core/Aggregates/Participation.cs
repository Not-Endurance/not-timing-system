using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using static NTS.Domain.Core.Aggregates.SnapshotResultType;

namespace NTS.Domain.Core.Aggregates;

public class Participation : Aggregate
{
    //static readonly TimeSpan NOT_SNAPSHOTABLE_WINDOW = TimeSpan.FromMinutes(30);
    static readonly FailedToQualify OUT_OF_TIME = new([FailToQualifyCode.OT]);
    static readonly FailedToQualify SPEED_RESTRICTION = new([FailToQualifyCode.SP]);

    public Participation(
        ParticipationCategory category,
        Competition competition,
        Combination combination,
        PhaseCollection phases,
        Eliminated? notQualified,
        int eventId,
        int id
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

    public int EventId { get; }
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
        return Combine(Combination, Phases, Eliminated);
    }

    //TODO rename to smthing better (including ISnapshotProcessor, IManualProcessor and other mentions..)
    public SnapshotResult Process(Snapshot snapshot)
    {
        var result = Phases.Process(snapshot, EventId);
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
        var qualificationRestored = new ParticipationRestored(this);
        Raise(qualificationRestored);
    }

    void EvaluatePhase(Phase phase)
    {
        if (phase.ViolatesRecoveryTime())
        {
            Eliminate(OUT_OF_TIME);
            return;
        }
        if (phase.ViolatesSpeedRestriction(Combination.MinAverageSpeed, Combination.MaxAverageSpeed))
        {
            Eliminate(SPEED_RESTRICTION);
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

        Phases.StartIfNext();
        var phaseCompleted = new PhaseCompleted(this);
        Raise(phaseCompleted);
    }

    void Eliminate(Eliminated notQualified)
    {
        Eliminated = notQualified;
        var qualificationRevoked = new ParticipationEliminated(this);
        Raise(qualificationRevoked);
    }
}
