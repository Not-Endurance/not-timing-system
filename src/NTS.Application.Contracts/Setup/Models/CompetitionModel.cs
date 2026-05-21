using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class CompetitionModel
{
    public static CompetitionModel MapFrom(Competition competition)
    {
        return new CompetitionModel
        {
            Id = competition.Id,
            Name = competition.Name,
            Type = competition.Type,
            Ruleset = competition.Ruleset,
            Start = competition.Start,
            CompulsoryThreshold = competition.CompulsoryThresholdSpan,
            FeiEventId = competition.FeiEventId,
            FeiEventCode = competition.FeiEventCode,
            FeiCompetitionId = competition.FeiCompetitionId,
            FeiRule = competition.FeiRule,
            FeiScheduleNumber = competition.FeiScheduleNumber,
            Phases = competition.Phases.Select(PhaseModel.Create).ToArray(),
            Participations = competition.Participations.Select(ParticipationModel.MapFrom).ToArray(),
        };
    }

    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public CompetitionType Type { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public DateTimeOffset? Start { get; init; }
    public TimeSpan? CompulsoryThreshold { get; init; }
    public string? FeiEventId { get; init; }
    public string? FeiEventCode { get; init; }
    public string? FeiCompetitionId { get; init; }
    public string? FeiRule { get; init; }
    public string? FeiScheduleNumber { get; init; }
    public PhaseModel[] Phases { get; init; } = default!;
    public ParticipationModel[] Participations { get; init; } = default!;

    public Competition MapToEntity()
    {
        var phases = Phases.Select(x => x.MapToEntity());
        var participations = Participations.Select(x => x.MapToEntity());
        return new Competition(
            Name,
            Type,
            Ruleset,
            Start,
            CompulsoryThreshold,
            FeiEventId,
            FeiEventCode,
            FeiCompetitionId,
            FeiRule,
            FeiScheduleNumber,
            phases,
            participations,
            Id
        );
    }
}
