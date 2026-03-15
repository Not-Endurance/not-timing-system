using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.StaticOptions;
using static NTS.Domain.Core.Aggregates.SnapshotResultType;

namespace NTS.Domain.Core.Aggregates.Participations.Entities;

public class Phase : Entity
{
    // TODO: settings - Add setting for separate final. This is useful for some events such as Shumen where we need separate detection for the actual final
    bool _isSeparateFinish = false;

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
        int? id = null
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
        ArriveTime = arriveTime;
        PresentTime = presentTime;
        RepresentTime = representTime;
        IsReinspectionRequested = isRepresentationRequested;
        IsRequiredInspectionRequested = isRequiredInspectionRequested;
        IsRequiredInspectionCompulsory = isRequiredInspectionCompulsory;
        CompulsoryThresholdSpan = compulsoryThresholdSpan;
    }

    Timestamp? VetTime => RepresentTime ?? PresentTime;

    public string Gate { get; private set; }
    public double Length { get; }
    public int MaxRecovery { get; }
    public int? Rest { get; }
    public CompetitionRuleset Ruleset { get; }
    public bool IsFinal { get; }
    public Timestamp? StartTime { get; internal set; } // TODO: does it have to be nullable?
    public Timestamp? ArriveTime { get; private set; }
    public Timestamp? PresentTime { get; private set; }
    public Timestamp? RepresentTime { get; private set; }
    public bool IsReinspectionRequested { get; internal set; } // TODO: rename to IsRepresentRequested
    public bool IsRequiredInspectionRequested { get; internal set; }
    public bool IsRequiredInspectionCompulsory { get; private set; }
    public TimeSpan? CompulsoryThresholdSpan { get; private set; }

    internal SnapshotResult Process(Snapshot snapshot)
    {
        return snapshot.Type switch
        {
            SnapshotType.Present => Inspect(snapshot),
            SnapshotType.Arrive => Arrive(snapshot),
            SnapshotType.Final => Finish(snapshot),
            SnapshotType.Automatic => Automatic(snapshot),
            _ => GuardUnknownSnapshot(snapshot),
        };
        static SnapshotResult GuardUnknownSnapshot(Snapshot snapshot)
        {
            var message = $"Invalid snapshot '{snapshot.GetType()}'";
            throw GuardHelper.Exception(message);
        }
    }

    internal void Update(IPhaseState state)
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
        ArriveTime = Timestamp.Create(state.ArriveTime);
        PresentTime = Timestamp.Create(state.PresentTime);
        RepresentTime = Timestamp.Create(state.RepresentTime);
        CheckCompulsoryThreshold();
    }

    internal bool ViolatesRecoveryTime()
    {
        return GetRecoveryInterval() > TimeSpan.FromMinutes(MaxRecovery);
    }

    internal bool ViolatesSpeedRestriction(Speed? minSpeed, Speed? maxSpeed)
    {
        var averageSpeed = GetAverageSpeed();
        return averageSpeed < minSpeed || averageSpeed > maxSpeed;
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
        IsRequiredInspectionRequested = true;
    }

    internal void RequireRepresentation()
    {
        if (PresentTime == null)
        {
            throw new DomainException(Cannot_require_representation_without_presentation_time);
        }
        IsReinspectionRequested = true;
    }

    internal void DisableRepresentation()
    {
        if (!IsReinspectionRequested)
        {
            return;
        }
        if (RepresentTime != null)
        {
            throw new DomainException(
                Cannot_disable_Reinspection_because_time_of_Reinspection_is_already_present_string
            );
        }
        IsReinspectionRequested = false;
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

    public Speed? GetAverageSpeed()
    {
        if (StaticOption.ShouldOnlyUseAverageLoopSpeed(Ruleset))
        {
            return GetAverageLoopSpeed();
        }
        return IsFinal ? GetAverageLoopSpeed() : GetAveragePhaseSpeed();
    }

    public bool IsComplete()
    {
        if (IsReinspectionRequested && RepresentTime == null)
        {
            return false;
        }
        if (ArriveTime == null || PresentTime == null)
        {
            return false;
        }
        return true;
    }

    SnapshotResult Automatic(Snapshot snapshot)
    {
        if (ArriveTime == null && IsFinal)
        {
            return Finish(snapshot);
        }
        if (ArriveTime == null)
        {
            return Arrive(snapshot);
        }
        if (PresentTime == null || RepresentTime == null && IsReinspectionRequested)
        {
            return Inspect(snapshot);
        }
        return SnapshotResult.NotApplied(snapshot, NotAppliedDueToInapplicableAutomatic);
    }

    SnapshotResult Finish(Snapshot snapshot)
    {
        if (_isSeparateFinish && !IsFinal)
        {
            return SnapshotResult.NotApplied(snapshot, NotAppliedDueToSeparateStageLine);
        }
        if (ArriveTime != null)
        {
            return SnapshotResult.NotApplied(snapshot, NotAppliedDueToDuplicateArrive);
        }
        if (snapshot.Timestamp < StartTime)
        {
            throw new DomainException(__cannot_be_sooner_than__string, Arrival_string, StartTime);
        }

        ArriveTime = snapshot.Timestamp;
        CheckCompulsoryThreshold();
        return SnapshotResult.Applied(snapshot);
    }

    SnapshotResult Arrive(Snapshot snapshot)
    {
        if (_isSeparateFinish && IsFinal)
        {
            return SnapshotResult.NotApplied(snapshot, NotAppliedDueToSeparateFinishLine);
        }
        if (ArriveTime != null)
        {
            return SnapshotResult.NotApplied(snapshot, NotAppliedDueToDuplicateArrive);
        }
        if (snapshot.Timestamp < StartTime)
        {
            throw new DomainException(__cannot_be_sooner_than__string, Arrival_string, StartTime);
        }

        ArriveTime = snapshot.Timestamp;
        CheckCompulsoryThreshold();
        return SnapshotResult.Applied(snapshot);
    }

    SnapshotResult Inspect(Snapshot snapshot)
    {
        if (IsReinspectionRequested && RepresentTime != null && PresentTime != null)
        {
            return SnapshotResult.NotApplied(snapshot, NotAppliedDueToDuplicateInspect);
        }
        if (snapshot.Timestamp <= ArriveTime)
        {
            throw new DomainException(__cannot_be_sooner_than__string, Presentation_string, ArriveTime);
        }

        if (IsReinspectionRequested)
        {
            if (snapshot.Timestamp <= PresentTime)
            {
                throw new DomainException(__cannot_be_sooner_than__string, Representation_string, PresentTime);
            }
            RepresentTime = snapshot.Timestamp;
        }
        else
        {
            PresentTime = snapshot.Timestamp;
        }
        CheckCompulsoryThreshold();
        return SnapshotResult.Applied(snapshot);
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
    int Id { get; }
    public DateTimeOffset? StartTime { get; }
    public DateTimeOffset? ArriveTime { get; }
    public DateTimeOffset? PresentTime { get; }
    public DateTimeOffset? RepresentTime { get; }
}
