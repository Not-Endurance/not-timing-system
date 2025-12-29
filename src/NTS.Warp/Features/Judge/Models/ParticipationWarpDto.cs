using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Warp.Features.Judge.Models;

public class ParticipationWarpDto
{
    public static ParticipationWarpDto Create(Domain.Setup.Aggregates.Participation participation)
    {
        return new ParticipationWarpDto
        {
            Id = participation.Id,
            Number = participation.Combination.Number,
            MinAverageSpeed = participation.MinAverageSpeed,
            MaxAverageSpeed = participation.MaxAverageSpeed,
            Athlete = new AthleteDto
            {
                Id = participation.Combination.Athlete.Id,
                Person = participation.Combination.Athlete.Names,
                Country = participation.Combination.Athlete.Country,
                Club = participation.Combination.Athlete.Club?.Name,
                FeiId = participation.Combination.Athlete.FeiId,
            },
            Horse = new HorseDto
            {
                Id = participation.Combination.Horse.Id,
                Name = participation.Combination.Horse.Name,
            },
        };
    }

    public static ParticipationWarpDto Create(Domain.Core.Aggregates.Participation participation)
    {
        return new ParticipationWarpDto
        {
            Id = participation.Id,
            Number = participation.Combination.Number,
            CompetitionName = participation.Competition.Name,
            Ruleset = participation.Competition.Ruleset,
            MinAverageSpeed = participation.Combination.MinAverageSpeed,
            MaxAverageSpeed = participation.Combination.MaxAverageSpeed,
            Athlete = new AthleteDto
            {
                //Id = participation.Combination.Athlete.Id,
                Person = participation.Combination.Athlete.Names,
                Country = participation.Combination.Athlete.Country,
                Club = participation.Combination.Athlete.Club?.Name,
                FeiId = participation.Combination.Athlete.FeiId,
            },
            Horse = new HorseDto
            {
                Id = participation.Combination.Horse.Id,
                Name = participation.Combination.Horse.Name,
            },
            Phases = participation
                .Phases.Select(x => new PhaseDto
                {
                    Id = x.Id,
                    Length = x.Length,
                    MaxRecovery = x.MaxRecovery,
                    Rest = x.Rest,
                    StartTime = x.StartTime,
                    ArriveTime = x.ArriveTime,
                    PresentTime = x.PresentTime,
                    RepresentTime = x.RepresentTime,
                    IsRequestedInspectionRequired = x.IsRequiredInspectionRequested,
                    IsRequestedInspectionCompulsory = x.IsRequiredInspectionCompulsory,
                })
                .ToArray(),
        };
    }

    public int Id { get; init; }
    public int Number { get; init; }
    public string? CompetitionName { get; init; }
    public CompetitionRuleset? Ruleset { get; init; }
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }
    public AthleteDto Athlete { get; init; } = default!;
    public HorseDto Horse { get; init; } = default!;
    public PhaseDto[]? Phases { get; init; } = [];

    public class AthleteDto
    {
        public int Id { get; init; }
        public string? Club { get; init; }
        public string? FeiId { get; init; }
        public Person Person { get; init; } = default!;
        public Country Country { get; init; } = default!;
    }

    public class HorseDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = default!;
        public string? FeiId { get; init; }
    }

    public class PhaseDto
    {
        public int Id { get; init; }
        public bool IsRequestedInspectionRequired { get; init; }
        public bool IsRequestedInspectionCompulsory { get; init; }
        public double Length { get; init; }
        public int MaxRecovery { get; init; }
        public int? Rest { get; init; }
        public DateTimeOffset? StartTime { get; init; }
        public DateTimeOffset? ArriveTime { get; init; }
        public DateTimeOffset? PresentTime { get; init; }
        public DateTimeOffset? RepresentTime { get; init; }
    }
}
