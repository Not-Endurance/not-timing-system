using Not.Krud.Abstractions;
using NTS.Application.Shared;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Application.Setup;

public class ClubModel : IDocument, IKrudModel<Club>
{
    public static ClubModel From(Club club)
    {
        var model = new ClubModel();
        model.MapFrom(club);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; set; } = default!;

    public Club MapToEntity()
    {
        return new Club(Name, Id);
    }

    public void MapFrom(Club club)
    {
        Id = club.Id;
        Name = club.Name;
    }
}

public class OfficialModel
{
    public static OfficialModel MapFrom(Official official)
    {
        return new OfficialModel
        {
            Id = official.Id,
            Names = official.Person.Names,
            Role = official.Role,
        };
    }

    public int Id { get; init; }
    public string[] Names { get; init; } = [];
    public OfficialRole Role { get; init; } = default!;

    public Official MapToEntity()
    {
        return new Official(Names, Role, Id);
    }
}

public class AthleteModel : IDocument, IKrudModel<Athlete>
{
    // TODO: if decide to use this approach integrate AutoMapper with specific mappings to solve duplicating mapping logic
    public static AthleteModel From(Athlete athlete)
    {
        var model = new AthleteModel();
        model.MapFrom(athlete);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string[] Names { get; set; } = default!;
    public CountryModel Country { get; set; } = default!;
    public ClubModel? Club { get; set; }
    public string? FeiId { get; set; }

    public void MapFrom(Athlete athlete)
    {
        Id = athlete.Id;
        FeiId = athlete.FeiId;
        Names = athlete.Names.Names;
        Country = CountryModel.From(athlete.Country);
        Club = athlete.Club == null ? null : ClubModel.From(athlete.Club);
    }

    public Athlete MapToEntity()
    {
        return new Athlete(Names, FeiId, Country?.MapToEntity(), Club?.MapToEntity(), Id);
    }
}

public class HorseModel : IDocument, IKrudModel<Horse>
{
    public static HorseModel From(Horse horse)
    {
        var model = new HorseModel();
        model.MapFrom(horse);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; set; } = default!;
    public string? FeiId { get; set; }

    public void MapFrom(Horse horse)
    {
        Id = horse.Id;
        Name = horse.Name;
        FeiId = horse.FeiId;
    }

    public Horse MapToEntity()
    {
        return new Horse(Name, FeiId, Id);
    }
}

public class CombinationModel
{
    public static CombinationModel MapFrom(Combination combination)
    {
        return new CombinationModel
        {
            Id = combination.Id,
            Number = combination.Number,
            Athlete = AthleteModel.From(combination.Athlete),
            Horse = HorseModel.From(combination.Horse),
        };
    }

    public int Id { get; init; }
    public int Number { get; init; }
    public AthleteModel Athlete { get; init; } = default!;
    public HorseModel Horse { get; init; } = default!;

    public Combination MapToEntity()
    {
        var athlete = Athlete.MapToEntity();
        var horse = Horse.MapToEntity();
        return new Combination(Number, athlete, horse, Id);
    }
}

public class ParticipationModel
{
    public static ParticipationModel MapFrom(Participation participation)
    {
        return new ParticipationModel
        {
            Id = participation.Id,
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

    public int Id { get; init; }
    public ParticipationCategory Category { get; init; } = default!;
    public CombinationModel Combination { get; init; } = default!;
    public bool IsNotRanked { get; init; }
    public DateTimeOffset? StartTimeOverride { get; init; }
    public double? MaxSpeedOverride { get; init; }
    public double? MinSpeedOverride { get; init; }
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }

    public Participation MapToEntity()
    {
        var combination = Combination.MapToEntity();
        return new Participation(
            IsNotRanked,
            combination,
            Category,
            StartTimeOverride,
            MaxSpeedOverride,
            MinSpeedOverride,
            MinAverageSpeed,
            MaxAverageSpeed,
            Id
        );
    }
}

public class LoopModel
{
    public static LoopModel MapFrom(Loop loop)
    {
        return new() { Id = loop.Id, Distance = loop.Distance };
    }

    public int Id { get; init; }
    public double Distance { get; init; }

    public Loop MapToEntity()
    {
        return new Loop(Distance, Id);
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

    public Phase MapToEntity()
    {
        var loop = Loop.MapToEntity();
        return new Phase(loop, Recovery, Rest, Id);
    }
}

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
            FeiId,
            FeiRule,
            FeiScheduleNumber,
            phases,
            participations,
            Id
        );
    }
}

public class UpcomingEventModel : IDocument, IKrudModel<UpcomingEvent>
{
    public static UpcomingEventModel From(UpcomingEvent @event)
    {
        var model = new UpcomingEventModel();
        model.MapFrom(@event);
        return model;
    }

    public int Id { get; set; } = default!;
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Place { get; set; } = default!;
    public CountryModel Country { get; set; } = default!;
    public string? ShowFeiId { get; set; }
    public string? FeiId { get; set; }
    public string? FeiEventCode { get; set; }
    public CompetitionModel[] Competitions { get; set; } = default!;
    public OfficialModel[] Officials { get; set; } = default!;
    public LoopModel[] Loops { get; set; } = default!;
    public CombinationModel[] Combinations { get; set; } = default!;
    public string Name { get; set; } = default!;

    public UpcomingEvent MapToEntity()
    {
        var country = Country.MapToEntity();
        var competitions = Competitions.Select(x => x.MapToEntity());
        var officials = Officials.Select(x => x.MapToEntity());
        var loops = Loops.Select(x => x.MapToEntity());
        var combinations = Combinations.Select(x => x.MapToEntity());
        return new UpcomingEvent(
            Name,
            Place,
            country,
            ShowFeiId,
            FeiId,
            FeiEventCode,
            competitions,
            officials,
            loops,
            combinations,
            Id
        );
    }

    public void MapFrom(UpcomingEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Place = @event.Place;
        Country = CountryModel.From(@event.Country);
        ShowFeiId = @event.ShowFeiId;
        FeiId = @event.FeiId;
        FeiEventCode = @event.FeiEventCode;
        Competitions = @event.Competitions.Select(CompetitionModel.MapFrom).ToArray();
        Officials = @event.Officials.Select(OfficialModel.MapFrom).ToArray();
        Loops = @event.Loops.Select(LoopModel.MapFrom).ToArray();
        Combinations = @event.Combinations.Select(CombinationModel.MapFrom).ToArray();
    }
}
