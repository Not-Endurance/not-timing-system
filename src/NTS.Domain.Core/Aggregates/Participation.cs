using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Events;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Objects.Payloads;
using static NTS.Domain.Core.Aggregates.SnapshotResultType;

namespace NTS.Domain.Core.Aggregates;

public class Participation : AggregateRoot, IAggregateRoot
{
    //static readonly TimeSpan NOT_SNAPSHOTABLE_WINDOW = TimeSpan.FromMinutes(30);
    static readonly FailedToQualify OUT_OF_TIME = new([FtqCode.OT]);
    static readonly FailedToQualify SPEED_RESTRICTION = new([FtqCode.SP]);
    public static readonly Event<PhaseCompleted> PHASE_COMPLETED_EVENT = new();
    public static readonly Event<ParticipationEliminated> ELIMINATED_EVENT = new();
    public static readonly Event<ParticipationRestored> RESTORED_EVENT = new();

    [JsonConstructor]
    Participation(
        int id,
        Competition competition,
        Combination combination,
        PhaseCollection phases,
        Eliminated? notQualified
    )
        : base(id)
    {
        Competition = competition;
        Combination = combination;
        Phases = phases;
        Eliminated = notQualified;
    }

    public Participation(
        string competitionName,
        CompetitionRuleset ruleset,
        CompetitionType type,
        Combination combination,
        IEnumerable<Phase> phases
    )
        : this(GenerateId(), new(competitionName, ruleset, type), combination, new(phases), null) { }

    public Competition Competition { get; }
    public Combination Combination { get; }
    public PhaseCollection Phases { get; }
    public Eliminated? Eliminated { get; private set; }

    public bool IsEliminated()
    {
        return Eliminated != null;
    }

    public bool IsComplete()
    {
        return Phases.All(x => x.IsComplete());
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
        if (Eliminated != null)
        {
            return SnapshotResult.NotApplied(snapshot, NotAppliedDueToNotQualified);
        }

        var result = Phases.Process(snapshot);
        EvaluatePhase(Phases.Current);

        return result;
    }

    public void Update(IPhaseState state)
    {
        var phase = Phases.FirstOrDefault(x => x.Id == state.Id);
        GuardHelper.ThrowIfDefault(phase);

        phase.Update(state);
        EvaluatePhase(phase);
    }

    public void ChangeReinspection(bool isRequested)
    {
        if (isRequested)
        {
            Phases.Current.IsReinspectionRequested = true;
        }
        else
        {
            Phases.Current.DisableReinspection();
        }
    }

    public void ChangeRequiredInspection(bool isRequested)
    {
        if (isRequested)
        {
            Phases.Current.RequestRequiredInspection();
        }
        else
        {
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

    public void Disqualify(string reason)
    {
        Eliminate(new Disqualified(reason));
    }

    public void FinishNotRanked(string reason)
    {
        Eliminate(new FinishedNotRanked(reason));
    }

    public void FailToQualify(FtqCode[] codes, string? reason)
    {
        Eliminate(new FailedToQualify(codes, reason));
    }

    public void Restore()
    {
        Eliminated = null;
        var qualificationRestored = new ParticipationRestored(this);
        RESTORED_EVENT.Emit(qualificationRestored);
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
        if (phase.IsComplete() && !phase.IsFinal)
        {
            Phases.StartIfNext();
            var phaseCompleted = new PhaseCompleted(this);
            PHASE_COMPLETED_EVENT.Emit(phaseCompleted);
        }
    }

    void Eliminate(Eliminated notQualified)
    {
        Eliminated = notQualified;
        var qualificationRevoked = new ParticipationEliminated(this);
        ELIMINATED_EVENT.Emit(qualificationRevoked);
    }
}
