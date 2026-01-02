using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Models;

public class SetupOfficialModel : IDocument
{
    public static SetupOfficialModel MapFrom(Official official)
    {
        return new SetupOfficialModel
        {
            Id = official.Id,
            Names = official.Person.Names,
            Role = official.Role,
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string[] Names { get; init; } = [];
    public OfficialRole Role { get; init; } = default!;

    public Official MapToDomain()
    {
        return new Official(Id, Names, Role);
    }
}

public class SetupAthleteModel : CoreAthleteModel, IDocument
{
    // TODO: if decide to use this approach integrate AutoMapper with specific mappings to solve duplicating mapping logic
    public static SetupAthleteModel MapFrom(Athlete athlete)
    {
        return new SetupAthleteModel
        {
            Id = athlete.Id,
            FeiId = athlete.FeiId,
            Names = athlete.Names,
            Country = CountryModel.MapFrom(athlete.Country),
            Club = athlete.Club == null ? null : ClubModel.MapFrom(athlete.Club),
        };
    }

    public int Id { get; init; }
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;

    public new Athlete MapToDomain()
    {
        var club = Club == null ? null : new Club(Club.Id, Club.Name);;
        return new Athlete(Id, Names, FeiId, Country?.MapToDomain(), club);
    }
}

public class SetupHorseModel : CoreHorseModel, IDocument
{
    public static SetupHorseModel MapFrom(Horse horse)
    {
        return new SetupHorseModel
        {
            Id = horse.Id,
            Name = horse.Name,
            FeiId = horse.FeiId,
        };
    }

    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;

    public new Horse MaptoDomain()
    {
        return new Horse(Id, Name, FeiId);
    }
}

public class SetupCombinationModel
{
    public static SetupCombinationModel MapFrom(Combination combination)
    {
        return new SetupCombinationModel
        {
            Id = combination.Id,
            Number = combination.Number,
            Athlete = SetupAthleteModel.MapFrom(combination.Athlete),
            Horse = SetupHorseModel.MapFrom(combination.Horse),
        };
    }

    public int Id { get; init; }
    public int Number { get; init; }
    public SetupAthleteModel Athlete { get; init; } = default!;
    public SetupHorseModel Horse { get; init; } = default!;

    public Combination MapToDomain()
    {
        var athlete = Athlete.MapToDomain();
        var horse = Horse.MaptoDomain();
        return new Combination(Id, Number, athlete, horse);
    }
}

public class SetupParticipationModel
{
    public static SetupParticipationModel MapFrom(Participation participation)
    {
        return new SetupParticipationModel
        {
            Id = participation.Id,
            IsNotRanked = participation.IsNotRanked,
            Category = participation.Category,
            Combination = SetupCombinationModel.MapFrom(participation.Combination),
            StartTimeOverride = participation.StartTimeOverride,
            MaxSpeedOverride = participation.MaxSpeedOverride,
            MinSpeedOverride = participation.MinSpeedOverride,
            MinAverageSpeed = participation.MinAverageSpeed,
            MaxAverageSpeed = participation.MaxAverageSpeed,
        };
    }

    public int Id { get; init; }
    public ParticipationCategory Category { get; init; } = default!;
    public SetupCombinationModel Combination { get; init; } = default!;
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
            Id,
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
        return new() { Id = loop.Id, Distance = loop.Distance };
    }

    public int Id { get; init; }
    public double Distance { get; init; }

    public Loop MapToDomain()
    {
        return Loop.Update(Id, Distance);
    }
}

public class SetupPhaseModel
{
    public static SetupPhaseModel Create(Phase phase)
    {
        return new SetupPhaseModel
        {
            Loop = LoopModel.MapFrom(phase.Loop!),
            Recovery = phase.Recovery,
            Rest = phase.Rest,
        };
    }

    public int Recovery { get; init; }
    public LoopModel Loop { get; init; } = default!;
    public int? Rest { get; init; }

    public Phase MapToDomain()
    {
        var loop = Loop.MapToDomain();
        return new Phase(loop, Recovery, Rest);
    }
}

public class SetupCompetitionModel
{
    public static SetupCompetitionModel MapFrom(Competition competition)
    {
        return new SetupCompetitionModel
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
            Phases = competition.Phases.Select(SetupPhaseModel.Create).ToArray(),
            Participations = competition.Participations.Select(SetupParticipationModel.MapFrom).ToArray(),
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
    public SetupPhaseModel[] Phases { get; init; } = default!;
    public SetupParticipationModel[] Participations { get; init; } = default!;

    public Competition MapToDomain()
    {
        var phases = Phases.Select(x => x.MapToDomain());
        var participations = Participations.Select(x => x.MapToDomain());
        return new Competition(
            Id,
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
            Competitions = @event.Competitions.Select(SetupCompetitionModel.MapFrom).ToArray(),
            Officials = @event.Officials.Select(SetupOfficialModel.MapFrom).ToArray(),
            Loops = @event.Loops.Select(LoopModel.MapFrom).ToArray(),
            Combinations = @event.Combinations.Select(SetupCombinationModel.MapFrom).ToArray(),
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string Place { get; init; } = default!;
    public CountryModel Country { get; init; } = default!;
    public string? ShowFeiId { get; init; }
    public string? FeiId { get; init; }
    public string? FeiEventCode { get; init; }
    public SetupCompetitionModel[] Competitions { get; init; } = default!;
    public SetupOfficialModel[] Officials { get; init; } = default!;
    public LoopModel[] Loops { get; init; } = default!;
    public SetupCombinationModel[] Combinations { get; init; } = default!;
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
