using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Application.Contracts.Setup.Models;

public class CompetitionModel
{
    public static CompetitionModel MapFrom(Competition competition)
    {
        return new CompetitionModel
        {
            Id = competition.Id,
            Name = competition.Name,
            EnglishNames = competition.EnglishNames,
            Start = competition.Start,
            CompulsoryThreshold = competition.CompulsoryThresholdSpan,
            MinSpeedRestriction = competition.MinSpeedRestriction,
            MaxSpeedRestriction = competition.MaxSpeedRestriction,
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
    public bool EnglishNames { get; init; }
    public DateTimeOffset? Start { get; init; }
    public TimeSpan? CompulsoryThreshold { get; init; }
    public double? MinSpeedRestriction { get; init; }
    public double? MaxSpeedRestriction { get; init; }
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
            EnglishNames,
            Start,
            CompulsoryThreshold,
            MinSpeedRestriction,
            MaxSpeedRestriction,
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
