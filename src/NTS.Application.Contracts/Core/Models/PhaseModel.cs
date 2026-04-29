using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Application.Contracts.Core.Models;

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
            ArriveTime = phase.ArriveTime,
            PresentTime = phase.PresentTime,
            RepresentTime = phase.RepresentTime,
            IsReinspectionRequested = phase.IsReinspectionRequested,
            IsRequiredInspectionRequested = phase.IsRequiredInspectionRequested || phase.IsRequiredInspectionCompulsory, // TODO: probably remove compulsory altogether
            IsRequiredInspectionCompulsory = phase.IsRequiredInspectionCompulsory,
            CompulsoryThresholdInterval = phase.CompulsoryThresholdSpan,
            RequiredInspectionTime = phase.GetRequiredInspectionTime(),
            OutTime = phase.GetOutTime(),
            LoopInterval = phase.GetLoopInterval(),
            PhaseInterval = phase.GetPhaseInterval(),
            RecoveryInterval = phase.GetRecoveryInterval(),
            AverageLoopSpeed = phase.GetAverageLoopSpeed(),
            AveragePhaseSpeed = phase.GetAveragePhaseSpeed(),
            AverageSpeed = phase.GetAverageSpeed(),
            IsComplete = phase.IsComplete(),
        };
    }

    public int Id { get; init; }
    public string Gate { get; init; } = default!;
    public double Length { get; init; }
    public int MaxRecovery { get; init; }
    public int? Rest { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public bool IsFinal { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? ArriveTime { get; init; }
    public DateTimeOffset? PresentTime { get; init; }
    public DateTimeOffset? RepresentTime { get; init; }
    public bool IsReinspectionRequested { get; init; }
    public bool IsRequiredInspectionRequested { get; init; }
    public bool IsRequiredInspectionCompulsory { get; init; }
    public DateTimeOffset? RequiredInspectionTime { get; init; }
    public DateTimeOffset? OutTime { get; init; }
    public TimeSpan? LoopInterval { get; init; }
    public TimeSpan? PhaseInterval { get; init; }
    public TimeSpan? RecoveryInterval { get; init; }
    public TimeSpan? CompulsoryThresholdInterval { get; init; } = TimeSpan.FromMinutes(10);
    public double? AverageLoopSpeed { get; init; }
    public double? AveragePhaseSpeed { get; init; }
    public double? AverageSpeed { get; init; }
    public bool IsComplete { get; init; }

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
            ArriveTime,
            PresentTime,
            RepresentTime,
            IsReinspectionRequested,
            IsRequiredInspectionRequested,
            IsRequiredInspectionCompulsory,
            Id
        );
    }
}
