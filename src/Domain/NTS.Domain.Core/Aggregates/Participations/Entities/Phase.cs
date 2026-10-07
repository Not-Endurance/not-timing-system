using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using static NTS.Domain.Core.Aggregates.Participations.Entities.TimeEventOutcome;

namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Phase : Entity
{
    readonly List<TimeEvent> _events;

    /// <summary>A Phase made with times: each of them is an accepted event of the manual method.</summary>
    public Phase(
        string gate,
        double length,
        int maxRecovery,
        int? rest,
        CompetitionRuleset ruleset,
        bool isFinal,
        TimeSpan? compulsoryThresholdSpan,
        Timestamp? startTime,
        Timestamp? arriveTime,
        Timestamp? presentTime,
        Timestamp? representTime,
        bool isRepresentationRequested,
        bool isRequiredInspectionRequested,
        bool isRequiredInspectionCompulsory,
        Guid? id = null
    )
        : this(
            gate,
            length,
            maxRecovery,
            rest,
            ruleset,
            isFinal,
            compulsoryThresholdSpan,
            startTime,
            EventsOfTimes(arriveTime, presentTime, representTime),
            isRepresentationRequested,
            isRequiredInspectionRequested,
            isRequiredInspectionCompulsory,
            id
        ) { }

    public Phase(
        string gate,
        double length,
        int maxRecovery,
        int? rest,
        CompetitionRuleset ruleset,
        bool isFinal,
        TimeSpan? compulsoryThresholdSpan,
        Timestamp? startTime,
        IEnumerable<TimeEvent> events,
        bool isRepresentationRequested,
        bool isRequiredInspectionRequested,
        bool isRequiredInspectionCompulsory,
        Guid? id = null
    )
        : base(id)
    {
        Gate = gate;
        Length = length;
        MaxRecovery = maxRecovery;
        Rest = rest;
        Ruleset = ruleset;
        IsFinal = isFinal;
        StartTime = startTime;
        _events = events.ToList();
        Events = _events.AsReadOnly();
        IsRepresentRequested = isRepresentationRequested;
        IsRequiredInspectionRequested = isRequiredInspectionRequested;
        IsRequiredInspectionCompulsory = isRequiredInspectionCompulsory;
        CompulsoryThresholdSpan = compulsoryThresholdSpan;
    }

    Timestamp? VetTime => RepresentTime ?? PresentTime;

    // TODO: settings - Add setting for separate final. This is useful for some events such as Shumen where we need separate detection for the actual final
    internal bool IsSeparateFinish { get; set; }

    public string Gate { get; private set; }
    public double Length { get; }
    public int MaxRecovery { get; }
    public int? Rest { get; }
    public CompetitionRuleset Ruleset { get; }
    public bool IsFinal { get; }
    public Timestamp? StartTime { get; internal set; } // TODO: does it have to be nullable?

    /// <summary>Every time the Phase received, accepted or not, in the order they were recorded.</summary>
    public IReadOnlyList<TimeEvent> Events { get; }

    // The times are the latest accepted event that feeds each of them (ADR-0005)
    public Timestamp? ArriveTime => Shown(TimeSlot.Arrive);
    public Timestamp? PresentTime => Shown(TimeSlot.Present);
    public Timestamp? RepresentTime => Shown(TimeSlot.Represent);
    public bool IsRepresentRequested { get; internal set; }
    public bool IsRequiredInspectionRequested { get; internal set; }
    public bool IsRequiredInspectionCompulsory { get; private set; }
    public TimeSpan? CompulsoryThresholdSpan { get; private set; }

    /// <summary>
    /// Records the Snapshot as a time event with the outcome that applies, whether it sets a time or not, and returns it. A
    /// time that cannot be set is rejected, and nothing is thrown for it.
    /// </summary>
    internal TimeEvent Process(Snapshot snapshot, Guid actorId, DateTimeOffset recordedAt)
    {
        TimeEvent recorded = snapshot.Type switch
        {
            SnapshotType.Present => Present(snapshot, actorId, recordedAt),
            SnapshotType.Arrive or SnapshotType.Final => Arrive(snapshot, actorId, recordedAt),
            _ => GuardUnknownSnapshot(snapshot),
        };
        _events.Add(recorded);
        if (recorded.IsAccepted)
        {
            CheckCompulsoryThreshold();
        }
        return recorded;
        static TimeEvent GuardUnknownSnapshot(Snapshot snapshot)
        {
            var message = $"Invalid snapshot '{snapshot.GetType()}'";
            throw GuardHelper.Exception(message);
        }
    }

    /// <summary>
    /// Records an Official's Update of a Snapshot they sent (ADR-0005, ADR-0013): the new time replaces the latest accepted
    /// time of the kind, whatever the Snapshot did, and the Update is an event of its own, accepted or not. An Arrive Update
    /// feeds the Arrive time. A Present Update changes the latest presentation the Phase accepted, so it feeds the Represent
    /// time when that was a representation and the Present time otherwise, and when there is none it is a presentation:
    /// it never makes a representation. A time that breaks the order is rejected as invalid, and the times stay. A time that
    /// is there may be corrected whether or not the Participation is complete.
    /// </summary>
    internal SnapshotUpdate UpdateTime(SnapshotType kind, Timestamp time, Guid actorId, DateTimeOffset recordedAt)
    {
        var slot = kind == SnapshotType.Present ? PresentationSlot() : TimeSlot.Arrive;
        var previous = Shown(slot);
        var outcome = IsInOrder(slot, time) ? Accepted : RejectedInvalidTime;
        TimeEvent recorded =
            slot == TimeSlot.Arrive
                ? new ArriveUpdated(time, outcome, SnapshotMethod.Manual, recordedAt, actorId)
                : new PresentUpdated(
                    time,
                    slot == TimeSlot.Represent,
                    outcome,
                    SnapshotMethod.Manual,
                    recordedAt,
                    actorId
                );
        _events.Add(recorded);
        if (recorded.IsAccepted)
        {
            CheckCompulsoryThreshold();
        }

        return new SnapshotUpdate(this, recorded, slot, previous, Shown(slot));
    }

    /// <summary>The state of the Phase form: each time that differs from the one the Phase shows becomes an accepted event.</summary>
    internal void Update(IPhaseState state, Guid actorId, DateTimeOffset recordedAt)
    {
        if (state.StartTime != null)
        {
            if (state.ArriveTime < state.StartTime)
            {
                throw new DomainPropertyException(
                    nameof(ArriveTime),
                    __cannot_be_sooner_than__string,
                    Arrival_string,
                    StartTime
                );
            }
            if (state.PresentTime < state.StartTime)
            {
                throw new DomainPropertyException(
                    nameof(PresentTime),
                    __cannot_be_sooner_than__string,
                    Presentation_string,
                    StartTime
                );
            }
            if (state.RepresentTime < state.ArriveTime)
            {
                throw new DomainPropertyException(
                    nameof(RepresentTime),
                    __cannot_be_sooner_than__string,
                    Presentation_string,
                    RepresentTime
                );
            }
            if (state.RepresentTime < state.PresentTime)
            {
                throw new DomainPropertyException(
                    nameof(RepresentTime),
                    __cannot_be_sooner_than__string,
                    Representation_string,
                    PresentTime
                );
            }
        }
        StartTime = Timestamp.Create(state.StartTime);
        Change(TimeSlot.Arrive, state.ArriveTime, actorId, recordedAt);
        Change(TimeSlot.Present, state.PresentTime, actorId, recordedAt);
        Change(TimeSlot.Represent, state.RepresentTime, actorId, recordedAt);
        CheckCompulsoryThreshold();
    }

    internal bool ViolatesRecoveryTime()
    {
        return GetRecoveryInterval() > TimeSpan.FromMinutes(MaxRecovery);
    }

    internal void RequestInspection()
    {
        if (IsRequiredInspectionRequested)
        {
            return;
        }
        if (IsRequiredInspectionCompulsory)
        {
            throw new DomainException(Required_inspection_is_compulsory_string);
        }
        if (IsRepresentRequested && RepresentTime == null)
        {
            throw new DomainException(Cannot_request_Required_Inspection_without_Representation_time_string);
        }
        IsRequiredInspectionRequested = true;
    }

    internal void RequireRepresentation()
    {
        if (PresentTime == null)
        {
            throw new DomainException(Cannot_require_representation_without_presentation_time);
        }
        IsRepresentRequested = true;
    }

    internal void DisableRepresentation()
    {
        if (!IsRepresentRequested)
        {
            return;
        }
        if (RepresentTime != null)
        {
            throw new DomainException(
                Cannot_disable_Reinspection_because_time_of_Reinspection_is_already_present_string
            );
        }
        IsRepresentRequested = false;
    }

    internal void SetGate(int number, double totalDistanceSoFar)
    {
        Gate = $"GATE{number}/{totalDistanceSoFar:0.##}";
    }

    public override string ToString()
    {
        var arrive = $"{ARR_string}:{ArriveTime}";
        var present = $"{IN_string}:{PresentTime}";
        var complete = IsComplete() ? complete_string : "";
        return Combine(Gate, arrive, present, complete);
    }

    public Timestamp? GetRequiredInspectionTime()
    {
        if (Rest == null)
        {
            return null;
        }
        var span = TimeSpan.FromMinutes(Rest.Value - 15); //TODO: settings
        return VetTime?.Add(span);
    }

    public Timestamp? GetOutTime()
    {
        if (ArriveTime == null || Rest == null)
        {
            return null;
        }
        var span = TimeSpan.FromMinutes(Rest.Value);
        return VetTime?.Add(span);
    }

    public TimeInterval? GetLoopInterval()
    {
        return ArriveTime - StartTime;
    }

    public TimeInterval? GetPhaseInterval()
    {
        return IsFinal ? GetLoopInterval() : VetTime - StartTime;
    }

    public TimeInterval? GetRecoveryInterval()
    {
        return VetTime - ArriveTime;
    }

    public Speed? GetAverageLoopSpeed()
    {
        return Length / GetLoopInterval();
    }

    public Speed? GetAveragePhaseSpeed()
    {
        return Length / GetPhaseInterval();
    }

    /// <summary>
    /// The average speed of the Phase, judged by the rules of the Event it belongs to (ADR-0012): a Regional competition
    /// may judge it on the loop alone. Asked without rules, it is judged as by an Event that has none.
    /// </summary>
    public Speed? GetAverageSpeed(RegionalRules? rules = null)
    {
        if (Ruleset == CompetitionRuleset.Regional && rules is { OnlyAverageLoopSpeed: true })
        {
            return GetAverageLoopSpeed();
        }
        return IsFinal ? GetAverageLoopSpeed() : GetAveragePhaseSpeed();
    }

    public bool IsComplete()
    {
        if (IsRepresentRequested && RepresentTime == null)
        {
            return false;
        }
        if (ArriveTime == null || PresentTime == null)
        {
            return false;
        }
        return true;
    }

    static IEnumerable<TimeEvent> EventsOfTimes(Timestamp? arriveTime, Timestamp? presentTime, Timestamp? representTime)
    {
        (TimeSlot Slot, Timestamp? Time)[] times =
        [
            (TimeSlot.Arrive, arriveTime),
            (TimeSlot.Present, presentTime),
            (TimeSlot.Represent, representTime),
        ];
        return times.Where(x => x.Time != null).Select(x => Manual(x.Slot, x.Time!, null, null));
    }

    /// <summary>An accepted event of the manual method, as the Phase form and a Phase made with times make it.</summary>
    static TimeEvent Manual(TimeSlot slot, Timestamp time, DateTimeOffset? recordedAt, Guid? actorId)
    {
        return slot == TimeSlot.Arrive
            ? new Arrived(time, Accepted, SnapshotMethod.Manual, recordedAt, actorId)
            : new Presented(time, slot == TimeSlot.Represent, Accepted, SnapshotMethod.Manual, recordedAt, actorId);
    }

    Arrived Arrive(Snapshot snapshot, Guid actorId, DateTimeOffset recordedAt)
    {
        var outcome = OutcomeOfArrive(snapshot.Timestamp, SeparateLineOutcome(snapshot.Type));
        return new Arrived(snapshot.Timestamp, outcome, snapshot.Method, recordedAt, actorId, snapshot.Id);
    }

    Presented Present(Snapshot snapshot, Guid actorId, DateTimeOffset recordedAt)
    {
        var slot = IsRepresentRequested ? TimeSlot.Represent : TimeSlot.Present;
        var outcome = OutcomeOfPresent(slot, snapshot.Timestamp);
        return new Presented(
            snapshot.Timestamp,
            slot == TimeSlot.Represent,
            outcome,
            snapshot.Method,
            recordedAt,
            actorId,
            snapshot.Id
        );
    }

    /// <summary>When the finish line is separate, the final Phase is arrived at by a final Snapshot and no other Phase is.</summary>
    TimeEventOutcome SeparateLineOutcome(SnapshotType type)
    {
        if (!IsSeparateFinish)
        {
            return Accepted;
        }
        if (type == SnapshotType.Final)
        {
            return IsFinal ? Accepted : RejectedSeparateStageLine;
        }
        return IsFinal ? RejectedSeparateFinishLine : Accepted;
    }

    TimeEventOutcome OutcomeOfArrive(Timestamp time, TimeEventOutcome line)
    {
        if (IsFinal && IsComplete())
        {
            return RejectedParticipationComplete;
        }
        if (line != Accepted)
        {
            return line;
        }
        if (ArriveTime != null)
        {
            return RejectedDuplicateArrive;
        }
        return IsInOrder(TimeSlot.Arrive, time) ? Accepted : RejectedInvalidTime;
    }

    TimeEventOutcome OutcomeOfPresent(TimeSlot slot, Timestamp time)
    {
        if (IsFinal && IsComplete())
        {
            return RejectedParticipationComplete;
        }
        if (IsRepresentRequested && RepresentTime != null && PresentTime != null)
        {
            return RejectedDuplicatePresent;
        }
        return IsInOrder(slot, time) ? Accepted : RejectedInvalidTime;
    }

    /// <summary>
    /// Start ≤ Arrive &lt; Presentation &lt; Representation, checked only among the times that exist: the time is in order when it
    /// does not fall short of the nearest time before it, nor reach the nearest one after it.
    /// </summary>
    bool IsInOrder(TimeSlot slot, Timestamp time)
    {
        return slot switch
        {
            TimeSlot.Arrive => !(time < StartTime) && !(time >= (PresentTime ?? RepresentTime)),
            TimeSlot.Present => !(time <= (ArriveTime ?? StartTime)) && !(time >= RepresentTime),
            _ => !(time <= (PresentTime ?? ArriveTime ?? StartTime)),
        };
    }

    /// <summary>The time a Present Update changes: the Represent time when the latest presentation accepted was a representation.</summary>
    TimeSlot PresentationSlot()
    {
        var latest = _events.LastOrDefault(x => x.IsAccepted && x.Slot != TimeSlot.Arrive);
        return latest?.Slot == TimeSlot.Represent ? TimeSlot.Represent : TimeSlot.Present;
    }

    Timestamp? Shown(TimeSlot slot)
    {
        return _events.LastOrDefault(x => x.IsAccepted && x.Slot == slot)?.Time;
    }

    /// <summary>A time that differs from the one shown is an accepted event; one that is cleared rejects the events that feed it.</summary>
    void Change(TimeSlot slot, DateTimeOffset? changed, Guid actorId, DateTimeOffset recordedAt)
    {
        var time = Timestamp.Create(changed);
        if (time == Shown(slot))
        {
            return;
        }
        if (time == null)
        {
            _events.Where(x => x.IsAccepted && x.Slot == slot).ToList().ForEach(x => x.RejectManually());
            return;
        }
        _events.Add(Manual(slot, time, recordedAt, actorId));
    }

    void CheckCompulsoryThreshold()
    {
        if (CompulsoryThresholdSpan == null || IsFinal)
        {
            return;
        }
        IsRequiredInspectionCompulsory = GetRecoveryInterval() >= CompulsoryThresholdSpan;
    }
}

public interface IPhaseState
{
    Guid Id { get; }
    public DateTimeOffset? StartTime { get; }
    public DateTimeOffset? ArriveTime { get; }
    public DateTimeOffset? PresentTime { get; }
    public DateTimeOffset? RepresentTime { get; }
}
