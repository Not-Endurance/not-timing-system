using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Storage.Documents.EnduranceEvents.Models;

public class PhaseModel
{
    public PhaseModel(Phase phase)
    {
        Gate = phase.Gate;
        Length = phase.Length;
        MaxRecovery = phase.MaxRecovery;
        Rest = phase.Rest;
        Ruleset = phase.Ruleset;
        IsFinal = phase.IsFinal;
        StartTime = phase.StartTime;
        ArriveTime = phase.ArriveTime;
        PresentTime = phase.PresentTime;
        RepresentTime = phase.RepresentTime;
        IsReinspectionRequested = phase.IsReinspectionRequested;
        IsRequiredInspectionRequested = phase.IsRequiredInspectionRequested;
    }

    public string Gate { get; init; }
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
}
