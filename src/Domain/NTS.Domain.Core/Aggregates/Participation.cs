using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;
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
    /// <summary>
    /// Places the Snapshot in a Phase by its time (see <see cref="PhaseAt"/>) and applies it there, once. A Snapshot stamped at
    /// or after the Start of the next Phase belongs to that Phase and makes it the current one.
    /// </summary>
    public SnapshotResult Process(Snapshot snapshot)
    {
        var result = Phases.Process(snapshot, EventId);
        if (Eliminated == null && result.Type == Applied)
        {
            EvaluatePhase(Phases.Current);
        }
        return result;
    }

    /// <summary>
    /// The Phase a Snapshot or a request made at the time belongs to: the next one when it has a Start and the time is at or
    /// after it, the current one otherwise (ADR-0004). The Representation and Inspection indicators of a screen describe this
    /// Phase, so that they show what a request made then would act on.
    /// <para>
    /// The time of a request is the clock of the server, and that of a Snapshot is the one its Official captured, so the clocks
    /// of the devices should agree with the server's to within about a minute around the Out time of a Phase. Times are
    /// compared by the time of the day, so a ride that crosses midnight is not supported.
    /// </para>
    /// </summary>
    public Phase PhaseAt(DateTimeOffset time)
    {
        return Phases.PhaseAt(new Timestamp(time));
    }

    public void Update(IPhaseState state)
    {
        var phase = Phases.FirstOrDefault(x => x.Id == state.Id);
        GuardHelper.ThrowIfDefault(phase);

        phase.Update(state);
        EvaluatePhase(phase);
    }

    /// <summary>
    /// A Representation is requested or withdrawn at the time, in the Phase that time belongs to (see <see cref="PhaseAt"/>),
    /// which becomes the current one when the request is taken.
    /// </summary>
    public void ToggleRepresentation(bool isRequested, DateTimeOffset at)
    {
        var phase = PhaseAt(at);
        if (isRequested)
        {
            phase.RequireRepresentation();
        }
        else
        {
            phase.DisableRepresentation();
        }

        Phases.Select(phase);
    }

    /// <summary>
    /// An Inspection is requested or withdrawn at the time, in the Phase that time belongs to (see <see cref="PhaseAt"/>),
    /// which becomes the current one when the request is taken.
    /// </summary>
    public void ToggleInspection(bool isRequested, DateTimeOffset at)
    {
        var phase = PhaseAt(at);
        if (isRequested)
        {
            phase.RequestInspection();
        }
        else
        {
            // TODO: rename to IsInspectionRequested
            phase.IsRequiredInspectionRequested = false;
        }

        Phases.Select(phase);
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
