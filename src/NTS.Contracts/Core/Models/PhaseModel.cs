using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;

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

    /// <summary>The times of the Phase are projected from its events, so they are not stored.</summary>
    public TimeEventModel[] Events { get; init; } = [];
    public bool IsRepresentRequested { get; init; }
    public bool IsRequiredInspectionRequested { get; init; }
    public bool IsRequiredInspectionCompulsory { get; init; }
    public TimeSpan? CompulsoryThresholdInterval { get; init; }

    public Phase MapToEntity()
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
            Events.Select(x => x.MapToEntity()),
            IsRepresentRequested,
            IsRequiredInspectionRequested,
            IsRequiredInspectionCompulsory,
            Id
        );
    }
}
