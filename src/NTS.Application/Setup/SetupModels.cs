using NTS.Application.Shared;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Application.Setup;

public class ClubModel : IDocument
{
    public static ClubModel MapFrom(Club club)
    {
        return new ClubModel { Id = club.Id, Name = club.Name };
    }

    public int Id { get; set; }
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; init; } = default!;

    public Club MapToDomain()
    {
        return new Club(Id, Name);
    }
}

public class OfficialModel
{
    public static OfficialModel MapFrom(Official official)
    {
        return new OfficialModel { Names = official.Person.Names, Role = official.Role };
    }

    public string[] Names { get; init; } = [];
    public OfficialRole Role { get; init; } = default!;

    public Official MapToDomain()
    {
        return new Official(Names, Role);
    }
}

public class AthleteModel : IDocument
{
    // TODO: if decide to use this approach integrate AutoMapper with specific mappings to solve duplicating mapping logic
    public static AthleteModel MapFrom(Athlete athlete)
    {
        return new AthleteModel
        {
            Id = athlete.Id,
            FeiId = athlete.FeiId,
            Names = athlete.Names.Names,
            Country = CountryModel.MapFrom(athlete.Country),
            Club = athlete.Club == null ? null : ClubModel.MapFrom(athlete.Club),
        };
    }

    public int Id { get; init; }
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string[] Names { get; init; } = default!;
    public CountryModel Country { get; init; } = default!;
    public ClubModel? Club { get; init; }
    public string? FeiId { get; init; }

    public Athlete MapToDomain()
    {
        return new Athlete(Id, Names, FeiId, Country?.MapToDomain(), Club?.MapToDomain());
    }
}

public class HorseModel : IDocument
{
    public static HorseModel MapFrom(Horse horse)
    {
        return new HorseModel
        {
            Id = horse.Id,
            Name = horse.Name,
            FeiId = horse.FeiId,
        };
    }

    public int Id { get; init; }
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public required string Name { get; init; }
    public string? FeiId { get; init; }

    public Horse MaptoDomain()
    {
        return new Horse(Id, Name, FeiId);
    }
}

public class CombinationModel
{
    public static CombinationModel MapFrom(Combination combination)
    {
        return new CombinationModel
        {
            Number = combination.Number,
            Athlete = AthleteModel.MapFrom(combination.Athlete),
            Horse = HorseModel.MapFrom(combination.Horse),
        };
    }

    public int Number { get; init; }
    public AthleteModel Athlete { get; init; } = default!;
    public HorseModel Horse { get; init; } = default!;

    public Combination MapToDomain()
    {
        var athlete = Athlete.MapToDomain();
        var horse = Horse.MaptoDomain();
        return new Combination(Number, athlete, horse);
    }
}

public class ParticipationModel
{
    public static ParticipationModel MapFrom(Participation participation)
    {
        return new ParticipationModel
        {
            IsNotRanked = participation.IsNotRanked,
            Category = participation.Category,
            Combination = CombinationModel.MapFrom(participation.Combination),
            StartTimeOverride = participation.StartTimeOverride,
            MaxSpeedOverride = participation.MaxSpeedOverride,
            MinSpeedOverride = participation.MinSpeedOverride,
            MinAverageSpeed = participation.MinAverageSpeed,
            MaxAverageSpeed = participation.MaxAverageSpeed,
        };
    }

    public ParticipationCategory Category { get; init; } = default!;
    public CombinationModel Combination { get; init; } = default!;
    public bool IsNotRanked { get; init; }
    public DateTimeOffset? StartTimeOverride { get; init; }
    public double? MaxSpeedOverride { get; init; }
    public double? MinSpeedOverride { get; init; }
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }

    public Participation MapToDomain()
    {
        var combination = Combination.MapToDomain();
        return new Participation(
            IsNotRanked,
            combination,
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
    public static LoopModel MapFrom(Loop loop)
    {
        return new() { Distance = loop.Distance };
    }

    public double Distance { get; init; }

    public Loop MapToDomain()
    {
        return new Loop(Distance);
    }
}

public class PhaseModel
{
    public static PhaseModel Create(Phase phase)
    {
        return new PhaseModel
        {
            Id = phase.Id,
            Loop = LoopModel.MapFrom(phase.Loop!),
            Recovery = phase.Recovery,
            Rest = phase.Rest,
        };
    }

    public int Id { get; set; }
    public int Recovery { get; init; }
    public LoopModel Loop { get; init; } = default!;
    public int? Rest { get; init; }

    public Phase MapToDomain()
    {
        var loop = Loop.MapToDomain();
        return new Phase(Id, loop, Recovery, Rest);
    }
}

public class CompetitionModel
{
    public static CompetitionModel MapFrom(Competition competition)
    {
        return new CompetitionModel
        {
            Name = competition.Name,
            Type = competition.Type,
            Ruleset = competition.Ruleset,
            Start = competition.Start,
            CompulsoryThreshold = competition.CompulsoryThresholdSpan,
            FeiId = competition.FeiId,
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
    public string? FeiId { get; init; }
    public string? FeiRule { get; init; }
    public string? FeiScheduleNumber { get; init; }
    public PhaseModel[] Phases { get; init; } = default!;
    public ParticipationModel[] Participations { get; init; } = default!;

    public Competition MapToDomain()
    {
        var phases = Phases.Select(x => x.MapToDomain());
        var participations = Participations.Select(x => x.MapToDomain());
        return new Competition(
            Name,
            Type,
            Ruleset,
            Start,
            CompulsoryThreshold,
            FeiId,
            FeiRule,
            FeiScheduleNumber,
            phases,
            participations
        );
    }
}

public class UpcomingEventModel : IDocument
{
    public static UpcomingEventModel MapFrom(UpcomingEvent @event)
    {
        return new UpcomingEventModel
        {
            Id = @event.Id,
            Name = @event.Name,
            Place = @event.Place,
            Country = CountryModel.MapFrom(@event.Country),
            ShowFeiId = @event.ShowFeiId,
            FeiId = @event.FeiId,
            FeiEventCode = @event.FeiEventCode,
            Competitions = @event.Competitions.Select(CompetitionModel.MapFrom).ToArray(),
            Officials = @event.Officials.Select(OfficialModel.MapFrom).ToArray(),
            Loops = @event.Loops.Select(LoopModel.MapFrom).ToArray(),
            Combinations = @event.Combinations.Select(CombinationModel.MapFrom).ToArray(),
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
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

    public UpcomingEvent MapToDomain()
    {
        var country = Country.MapToDomain();
        var competitions = Competitions.Select(x => x.MapToDomain());
        var officials = Officials.Select(x => x.MapToDomain());
        var loops = Loops.Select(x => x.MapToDomain());
        var combinations = Combinations.Select(x => x.MapToDomain());
        return new UpcomingEvent(
            Id,
            Name,
            Place,
            country,
            ShowFeiId,
            FeiId,
            FeiEventCode,
            competitions,
            officials,
            loops,
            combinations
        );
    }
}
