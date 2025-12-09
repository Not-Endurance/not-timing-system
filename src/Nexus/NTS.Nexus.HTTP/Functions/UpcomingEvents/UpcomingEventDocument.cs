using Not.Random;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Application.DataTransferObjects;

namespace NTS.Nexus.HTTP.Functions.UpcomingEvents;

public class UpcomingEventDocument : Identity
{
    public static UpcomingEventDocument Create(UpcomingEvent evt)
    {
        return new UpcomingEventDocument
        {
            Id = evt.Id,
            Name = evt.Name,
            Place = evt.Place,
            Country = CountryModel.Create(evt.Country),
            ShowFeiId = evt.ShowFeiId,
            FeiId = evt.FeiId,
            FeiEventCode = evt.FeiEventCode,
            Competitions = evt.Competitions.Select(CompetitionModel.Create).ToArray(),
            Officials = evt.Officials.Select(OfficialModel.Create).ToArray(),
            Loops = evt.Loops.Select(LoopModel.Create).ToArray(),
            Combinations = evt.Combinations.Select(CombinationModel.Create).ToArray(),
        };
    }

    public string Place { get; init; } = default!;
    public CountryModel Country { get; init; } = default!;
    public string? ShowFeiId { get; init; }
    public string? FeiId { get; init; }
    public string? FeiEventCode { get; init; }
    public CompetitionModel[] Competitions { get; init; } = default!;
    public OfficialModel[] Officials { get; init; } = default!;
    public LoopModel[] Loops { get; init; } = default!;
    public CombinationModel[] Combinations { get; init; } = default!;
    public string Name { get; init; } = default!;

    public UpcomingEvent ToDomain()
    {
        return new UpcomingEvent(
            Id,
            Name,
            Place,
            Country.ToDomain(),
            ShowFeiId,
            FeiId,
            FeiEventCode,
            Competitions.Select(x => x.ToSetupDomain()),
            Officials.Select(x => x.ToSetupDomain()),
            Loops.Select(x => x.ToSetupDomain()),
            Combinations.Select(x => x.ToSetupDomain())
        );
    }

    static int EnsureId(int id)
    {
        return id == default ? RandomHelper.GenerateUniqueInteger() : id;
    }

    public class CompetitionModel
    {
        public static CompetitionModel Create(Competition competition)
        {
            return new CompetitionModel
            {
                Id = competition.Id,
                Name = competition.Name,
                Type = competition.Type,
                Ruleset = competition.Ruleset,
                Start = competition.Start,
                CompulsoryThreshold = competition.CompulsoryThresholdSpan,
                FeiId = competition.FeiId,
                FeiRule = competition.FeiRule,
                FeiScheduleNumber = competition.FeiScheduleNumber,
                Phases = competition.Phases.Select(PhaseModel.Create).ToArray(),
                Participations = competition.Participations.Select(ParticipationModel.Create).ToArray(),
            };
        }

        public int Id { get; init; }
        public string Name { get; init; } = default!;
        public CompetitionType Type { get; init; }
        public CompetitionRuleset Ruleset { get; init; }
        public DateTimeOffset Start { get; init; }
        public TimeSpan? CompulsoryThreshold { get; init; }
        public string? FeiId { get; init; }
        public string? FeiRule { get; init; }
        public string? FeiScheduleNumber { get; init; }
        public PhaseModel[] Phases { get; init; } = default!;
        public ParticipationModel[] Participations { get; init; } = default!;

        public Competition ToSetupDomain()
        {
            return new Competition(
                EnsureId(Id),
                Name,
                Type,
                Ruleset,
                Start,
                CompulsoryThreshold,
                FeiId,
                FeiRule,
                FeiScheduleNumber,
                Phases.Select(x => x.ToSetupDomain()),
                Participations.Select(x => x.ToSetupDomain())
            );
        }
    }

    public class PhaseModel
    {
        public static PhaseModel Create(Phase phase)
        {
            return new PhaseModel
            {
                Id = phase.Id,
                Loop = LoopModel.Create(phase.Loop!),
                Recovery = phase.Recovery,
                Rest = phase.Rest,
            };
        }

        public int Id { get; init; }
        public LoopModel Loop { get; init; } = default!;
        public int Recovery { get; init; }
        public int? Rest { get; init; }

        public Phase ToSetupDomain()
        {
            return Phase.Update(EnsureId(Id), Loop.ToSetupDomain(), Recovery, Rest);
        }
    }

    public class ParticipationModel
    {
        public static ParticipationModel Create(Participation participation)
        {
            return new ParticipationModel
            {
                Id = participation.Id,
                StartTimeOverride = participation.StartTimeOverride,
                IsNotRanked = participation.IsNotRanked,
                Category = participation.Category,
                Combination = CombinationModel.Create(participation.Combination),
                MaxSpeedOverride = participation.MaxSpeedOverride,
                MinSpeedOverride = participation.MinSpeedOverride,
                MinAverageSpeed = participation.MinAverageSpeed,
                MaxAverageSpeed = participation.MaxAverageSpeed,
            };
        }

        public int Id { get; init; }
        public DateTimeOffset? StartTimeOverride { get; init; }
        public bool IsNotRanked { get; init; }
        public CombinationModel Combination { get; init; } = default!;
        public ParticipationCategory Category { get; set; }
        public double? MaxSpeedOverride { get; init; }
        public double? MinSpeedOverride { get; init; }
        public double? MinAverageSpeed { get; init; }
        public double? MaxAverageSpeed { get; init; }

        public Participation ToSetupDomain()
        {
            return new(
                EnsureId(Id),
                IsNotRanked,
                Combination.ToSetupDomain(),
                Category,
                StartTimeOverride,
                MaxSpeedOverride,
                MinSpeedOverride,
                MinAverageSpeed,
                MaxAverageSpeed
            );
        }
    }

    public class LoopModel
    {
        public static LoopModel Create(Loop loop)
        {
            return new() { Id = loop.Id, Distance = loop.Distance };
        }

        public int Id { get; init; }
        public double Distance { get; init; }

        public Loop ToSetupDomain()
        {
            return Loop.Update(EnsureId(Id), Distance);
        }
    }

    public class CombinationModel
    {
        public static CombinationModel Create(Combination combination)
        {
            return new CombinationModel
            {
                Id = combination.Id,
                Number = combination.Number,
                Athlete = AthleteModel.Create(combination.Athlete),
                Horse = HorseModel.Create(combination.Horse),
            };
        }

        public int Id { get; init; }
        public int Number { get; init; }
        public AthleteModel Athlete { get; init; } = default!;
        public HorseModel Horse { get; init; } = default!;

        public Combination ToSetupDomain()
        {
            return Combination.Update(EnsureId(Id), Number, Athlete.ToSetupDomain(), Horse.ToSetupDomain(), null);
        }
    }
}
