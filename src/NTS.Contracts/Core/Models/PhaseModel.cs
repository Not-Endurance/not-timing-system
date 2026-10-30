using MongoDB.Bson.Serialization.Attributes;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Core.Models;

public class PhaseModel
{
    public static PhaseModel MapFrom(Phase phase)
    {
        return new PhaseModel
        {
            Id = phase.Id,
            Gate = phase.Gate,
            Length = phase.Length,
            MaxRecovery = phase.MaxRecovery,
            Rest = phase.Rest,
            Ruleset = phase.Ruleset,
            IsFinal = phase.IsFinal,
            StartTime = phase.StartTime,
            Events = phase.Events.Select(TimeEventModel.MapFrom).ToArray(),
            IsRepresentRequested = phase.IsRepresentRequested,
            IsRequiredInspectionRequested = phase.IsRequiredInspectionRequested || phase.IsRequiredInspectionCompulsory, // TODO: probably remove compulsory altogether
            IsRequiredInspectionCompulsory = phase.IsRequiredInspectionCompulsory,
            CompulsoryThresholdInterval = phase.CompulsoryThresholdSpan,
        };
    }

    public Guid Id { get; init; }
    public string Gate { get; init; } = default!;
    public double Length { get; init; }
    public int MaxRecovery { get; init; }
    public int? Rest { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public bool IsFinal { get; init; }
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// The times of the Phase are projected from its events, so they are not stored. A Phase that is read by someone who may not
    /// see how its times were recorded has none (ADR-0005, ADR-0012): it tells its times in <see cref="ArriveTime"/>,
    /// <see cref="PresentTime"/> and <see cref="RepresentTime"/>.
    /// </summary>
    public TimeEventModel[] Events { get; init; } = [];

    /// <summary>The Arrive time the Phase shows, told apart from the events that made it. It is not stored, and it is read only when there are no events.</summary>
    [BsonIgnore]
    public DateTimeOffset? ArriveTime { get; init; }

    /// <summary>The Present time the Phase shows, told apart from the events that made it (see <see cref="ArriveTime"/>).</summary>
    [BsonIgnore]
    public DateTimeOffset? PresentTime { get; init; }

    /// <summary>The Represent time the Phase shows, told apart from the events that made it (see <see cref="ArriveTime"/>).</summary>
    [BsonIgnore]
    public DateTimeOffset? RepresentTime { get; init; }
    public bool IsRepresentRequested { get; init; }
    public bool IsRequiredInspectionRequested { get; init; }
    public bool IsRequiredInspectionCompulsory { get; init; }
    public TimeSpan? CompulsoryThresholdInterval { get; init; }

    /// <summary>
    /// The Phase as it is read (ADR-0005, ADR-0012): it tells the times it shows, which is what anybody may read, and how they
    /// were recorded, its events, only to someone who may see that. Its times are what its events make them, so a Phase read
    /// back from a Phase that tells no events shows the same times.
    /// </summary>
    public PhaseModel ShownTo(bool seesTimeEvents)
    {
        var phase = MapToEntity();
        return new PhaseModel
        {
            Id = Id,
            Gate = Gate,
            Length = Length,
            MaxRecovery = MaxRecovery,
            Rest = Rest,
            Ruleset = Ruleset,
            IsFinal = IsFinal,
            StartTime = StartTime,
            Events = seesTimeEvents ? Events : [],
            ArriveTime = phase.ArriveTime?.ToDateTimeOffset(),
            PresentTime = phase.PresentTime?.ToDateTimeOffset(),
            RepresentTime = phase.RepresentTime?.ToDateTimeOffset(),
            IsRepresentRequested = IsRepresentRequested,
            IsRequiredInspectionRequested = IsRequiredInspectionRequested,
            IsRequiredInspectionCompulsory = IsRequiredInspectionCompulsory,
            CompulsoryThresholdInterval = CompulsoryThresholdInterval,
        };
    }

    public Phase MapToEntity()
    {
        if (Events.Length == 0 && (ArriveTime != null || PresentTime != null || RepresentTime != null))
        {
            return new Phase(
                Gate,
                Length,
                MaxRecovery,
                Rest,
                Ruleset,
                IsFinal,
                CompulsoryThresholdInterval,
                StartTime,
                Timestamp.Create(ArriveTime),
                Timestamp.Create(PresentTime),
                Timestamp.Create(RepresentTime),
                IsRepresentRequested,
                IsRequiredInspectionRequested,
                IsRequiredInspectionCompulsory,
                Id
            );
        }

        return new Phase(
            Gate,
            Length,
            MaxRecovery,
            Rest,
            Ruleset,
            IsFinal,
            CompulsoryThresholdInterval,
            StartTime,
            Events.Select(x => x.MapToEntity()),
            IsRepresentRequested,
            IsRequiredInspectionRequested,
            IsRequiredInspectionCompulsory,
            Id
        );
    }
}
