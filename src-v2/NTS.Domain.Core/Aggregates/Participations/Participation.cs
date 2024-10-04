using Not.Events;
using NTS.Domain.Core.Events.Participations;
using static NTS.Domain.Core.Aggregates.Participations.SnapshotResultType;

namespace NTS.Domain.Core.Aggregates.Participations;

public class Participation : DomainEntity, IAggregateRoot
{
    private static readonly TimeSpan NOT_SNAPSHOTABLE_WINDOW = TimeSpan.FromMinutes(30);
    private static readonly FailedToQualify OUT_OF_TIME = new ([FTQCodes.OT]);
    private static readonly FailedToQualify SPEED_RESTRICTION = new ([FTQCodes.SP]);

    private Participation(int id) : base(id)
    {
    }
    public Participation(string competitionName, CompetitionRuleset ruleset, Tandem tandem, IEnumerable<Phase> phases)
    {
        Competition = new Competition(competitionName, ruleset);
        Tandem = tandem;
        Phases = new(phases);
    }

    public Competition Competition { get; private set; }
    public Tandem Tandem { get; private set; }
    public PhaseCollection Phases { get; private set; }
    public NotQualified? NotQualified { get; private set; }
    
    public bool IsNotQualified()
    {
        return NotQualified != null;
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
        if (NotQualified != null)
        {
            result += $", {NotQualified}";
        }
        return result;
    }

    public SnapshotResult Process(Snapshot snapshot)
    {
        if (NotQualified != null)
        {
            return SnapshotResult.NotApplied(snapshot, NotAppliedDueToNotQualified);
        }

        var result = Phases.Current.Process(snapshot);
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
        if (!isRequested)
        {
            Phases.Current.DisableReinspection();
        }
        else
        {
            Phases.Current.IsReinspectionRequested = true;
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
            Phases.Current.IsRIRequested = isRequested;
        }
        Phases.Current.IsReinspectionRequested = isRequested;
    }

    public void Withdraw()
    {
        RevokeQualification(new Withdrawn());
    }
    public void Retire()
    {
        RevokeQualification(new Retired());
    }

    public void Disqualify(string reason)
    {
        RevokeQualification(new Disqualified(reason));
    }

    public void FinishNotRanked(string reason)
    {
        RevokeQualification(new FinishedNotRanked(reason));
    }

    public void FailToQualify(params FTQCodes[] codes)
    {
        RevokeQualification(new FailedToQualify(codes));
    }

    public void FailToQualify(string? reason, params FTQCodes[] codes)
    {
        if(reason == null)
        {
            RevokeQualification(new FailedToQualify(codes));
        }
        else
        {
            RevokeQualification(new FailedToQualify(codes, reason));
        }
    }

    private void EvaluatePhase(Phase phase)
    {
        if (phase.ViolatesRecoveryTime())
        {
            RevokeQualification(OUT_OF_TIME);
            return;
        }
        if (phase.ViolatesSpeedRestriction(Tandem.MinAverageSpeed, Tandem.MaxAverageSpeed))
        {
            RevokeQualification(SPEED_RESTRICTION);
            return;
        }
        if (NotQualified == OUT_OF_TIME || NotQualified == SPEED_RESTRICTION)
        {
            RestoreQualification();
        }
        if (phase.IsComplete())
        {
            var phaseCompleted = new PhaseCompleted(this);
            EventHelper.Emit(phaseCompleted);
            Phases.StartNext();
        }
    }

    private void RevokeQualification(NotQualified notQualified)
    {
        NotQualified = notQualified;
        var qualificationRevoked = new QualificationRevoked(this);
        EventHelper.Emit(qualificationRevoked);
    }
    public void RestoreQualification()
    {
        NotQualified = null;
        var qualificationRestored = new QualificationRestored(this);
        EventHelper.Emit(qualificationRestored);
    }
}
