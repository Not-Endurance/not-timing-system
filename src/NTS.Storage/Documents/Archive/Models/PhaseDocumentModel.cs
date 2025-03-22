using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.Archive.Models;

public class PhaseDocumentModel
{
    public PhaseDocumentModel(Phase phase)
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
        RequiredInspectionTime = phase.GetRequiredInspectionTime();
        OutTime = phase.GetOutTime();
        LoopInterval = phase.GetLoopInterval();
        PhaseInterval = phase.GetPhaseInterval();
        RecoveryInterval = phase.GetRecoveryInterval();
        AverageLoopSpeed = phase.GetAverageLoopSpeed();
        AveragePhaseSpeed = phase.GetAveragePhaseSpeed();
        AverageSpeed = phase.GetAverageSpeed();
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
    public DateTimeOffset? RequiredInspectionTime { get; init; }
    public DateTimeOffset? OutTime { get; init; }
    public TimeSpan? LoopInterval { get; init; }
    public TimeSpan? PhaseInterval { get; init; }
    public TimeSpan? RecoveryInterval { get; init; }
    public double? AverageLoopSpeed { get; init; }
    public double? AveragePhaseSpeed { get; init; }
    public double? AverageSpeed { get; init; }
}
