using Not.Events;
using NTS.Domain.Core.Entities.ParticipationAggregate;
using NTS.Domain.Core.Objects.Payloads;
using static NTS.Domain.Core.Entities.SnapshotResultType;

namespace NTS.Domain.Core.Entities;

public class Participation : DomainEntity, IAggregateRoot
{
    public readonly static Event<PhaseCompleted> PhaseCompletedEvent = new();
    public readonly static Event<ParticipationEliminated> EliminatedEvent = new();
    public readonly static Event<ParticipationRestored> RestoredEvent = new();

    private static readonly TimeSpan NOT_SNAPSHOTABLE_WINDOW = TimeSpan.FromMinutes(30);
    private static readonly FailedToQualify OUT_OF_TIME = new([FTQCodes.OT]);
    private static readonly FailedToQualify SPEED_RESTRICTION = new([FTQCodes.SP]);

    private Participation(int id, Competition competition, Tandem tandem, PhaseCollection phases, Eliminated? notQualified) : base(id)
    {
        Competition = competition;
        Tandem = tandem;
        Phases = phases;
        Eliminated = notQualified;
    }
    public Participation(string competitionName, CompetitionRuleset ruleset, Tandem tandem, IEnumerable<Phase> phases)
        : this(
              GenerateId(),
              new(competitionName, ruleset),
              tandem,
              new(phases),
              null)
    {
    }

    public Competition Competition { get; private set; }
    public Tandem Tandem { get; private set; }
    public PhaseCollection Phases { get; private set; }
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
        var result = $"{Tandem}, {Phases}";
        if (Eliminated != null)
        {
            result += $", {Eliminated}";
        }
        return result;
    }

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

    public void FailToQualify(FTQCodes[] codes, string? reason)
    {
        Eliminate(new FailedToQualify(codes, reason));
    }

    private void EvaluatePhase(Phase phase)
    {
        if (phase.ViolatesRecoveryTime())
        {
            Eliminate(OUT_OF_TIME);
            return;
        }
        if (phase.ViolatesSpeedRestriction(Tandem.MinAverageSpeed, Tandem.MaxAverageSpeed))
        {
            Eliminate(SPEED_RESTRICTION);
            return;
        }
        if (Eliminated == OUT_OF_TIME || Eliminated == SPEED_RESTRICTION)
        {
            Restore();
        }
        if (phase.IsComplete())
        {
            var phaseCompleted = new PhaseCompleted(this);
            PhaseCompletedEvent.Emit(phaseCompleted);
            Phases.StartIfNext();
        }
    }

    private void Eliminate(Eliminated notQualified)
    {
        Eliminated = notQualified;
        var qualificationRevoked = new ParticipationEliminated(this);
        EliminatedEvent.Emit(qualificationRevoked);
    }

    public void Restore()
    {
        Eliminated = null;
        var qualificationRestored = new ParticipationRestored(this);
        RestoredEvent.Emit(qualificationRestored);
    }
}
