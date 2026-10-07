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
        Guid? id = null,
        int version = 0
    )
        : base(id)
    {
        EventId = eventId;
        Version = version;
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

    /// <summary>
    /// How many times the stored Participation has been written (ADR-0013): what a view of it was read at, so that a write
    /// made from it can be told from a write made from the current one. The domain does not change it; the store does.
    /// </summary>
    public int Version { get; }

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

    /// <summary>
    /// An Official corrects the time of a Snapshot they sent (ADR-0005, ADR-0013). The Snapshot names the kind of time, an
    /// Arrive or a Present, and not the Phase: the Update is recorded in the Phase its new time belongs to (see
    /// <see cref="PhaseAt"/>), replaces the latest accepted time of that kind there, and is an event of its own whatever
    /// becomes of it, so that what was refused is not lost. The last write wins. An accepted one is evaluated like any
    /// accepted change (see <see cref="Phase"/>): the Participation is eliminated for time or restored, the Phase completes,
    /// and the next one starts when it is out. An elimination that a person gave is left as it is.
    /// </summary>
    /// <param name="snapshotId">The id of the Snapshot that was sent, which is the id of the event that recorded it.</param>
    /// <param name="time">The time it should have had.</param>
    /// <param name="actorId">The signed-in user that corrects it.</param>
    /// <param name="recordedAt">The instant the server recorded the correction.</param>
    public SnapshotUpdate UpdateSnapshot(Guid snapshotId, Timestamp time, Guid actorId, DateTimeOffset recordedAt)
    {
        var sent =
            Phases.SelectMany(x => x.Events).FirstOrDefault(x => x.Id == snapshotId)
            ?? throw GuardHelper.Exception($"Participation {Id} holds no Snapshot {snapshotId} to update.");
        var kind = sent is Presented or PresentUpdated ? SnapshotType.Present : SnapshotType.Arrive;
        var phase = Phases.PhaseAt(time);
        var outBefore = phase.GetOutTime();
        var update = phase.UpdateTime(kind, time, actorId, recordedAt);
        if (!update.IsAccepted)
        {
            return update;
        }

        Phases.Select(phase);
        if (Eliminated == null || IsMadeByEvaluation(Eliminated))
        {
            Reevaluate(phase, outBefore);
        }

        return update;
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
        if (IsMadeByEvaluation(Eliminated))
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

    /// <summary>
    /// Whether the elimination is one the evaluation made, for time or for the speed restriction, and may lift when the times
    /// no longer call for it. One that was stored and read again is an instance of its own, so it is told by its codes and
    /// not by being the one instance this class holds, and one that a person gave, with a reason, is not the evaluation's.
    /// </summary>
    static bool IsMadeByEvaluation(Eliminated? eliminated)
    {
        return eliminated is FailedToQualify { Complement: null } failed
            && (
                failed.FtqCodes.SequenceEqual(OUT_OF_TIME.FtqCodes)
                || failed.FtqCodes.SequenceEqual(SPEED_RESTRICTION.FtqCodes)
            );
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
