using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;

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
    /// Places the Snapshot in a Phase by its time (see <see cref="PhaseAt"/>) and records it there as a time event, once,
    /// whatever its outcome: a time that cannot be set is recorded as rejected and nothing is thrown. A Snapshot stamped at or
    /// after the Start of the next Phase belongs to that Phase, and when it is accepted that Phase becomes the current one.
    /// </summary>
    /// <param name="snapshot">The time that was captured.</param>
    /// <param name="actorId">The signed-in user that recorded it.</param>
    /// <param name="recordedAt">The instant the server recorded it.</param>
    public TimeEvent Process(Snapshot snapshot, Guid actorId, DateTimeOffset recordedAt)
    {
        var phase = Phases.PhaseAt(snapshot.Timestamp);
        var outBefore = phase.GetOutTime();
        var recorded = phase.Process(snapshot, actorId, recordedAt);
        if (!recorded.IsAccepted)
        {
            return recorded;
        }

        Phases.Select(phase);
        if (Eliminated == null)
        {
            Reevaluate(phase, outBefore);
        }
        return recorded;
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

    /// <summary>The Phase form: each time of the Phase that the state changes is recorded as an accepted time event.</summary>
    public void Update(IPhaseState state, Guid actorId, DateTimeOffset recordedAt)
    {
        var phase = Phases.FirstOrDefault(x => x.Id == state.Id);
        GuardHelper.ThrowIfDefault(phase);

        var outBefore = phase.GetOutTime();
        phase.Update(state, actorId, recordedAt);
        Reevaluate(phase, outBefore);
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
        Phases.StartNextAfter(completed);
        Raise(Completed(completed));
    }

    /// <summary>
    /// What follows from a Phase's accepted change, whichever way it was made and whether or not the Phase is the current one:
    /// eliminated for time when the recovery is over the limit, or restored when it no longer is, the Phase after it starting
    /// when it is out, in place when its Out time moved, and the completion announced once when the Phase is complete.
    /// </summary>
    void Reevaluate(Phase phase, Timestamp? outBefore)
    {
        if (phase.ViolatesRecoveryTime())
        {
            Eliminate(OUT_OF_TIME);
            return;
        }
        if (Eliminated == OUT_OF_TIME || Eliminated == SPEED_RESTRICTION)
        {
            Eliminated = null;
        }
        if (!phase.IsComplete())
        {
            return;
        }

        if (phase.GetOutTime() != outBefore)
        {
            Phases.StartNextAfter(phase);
        }
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
