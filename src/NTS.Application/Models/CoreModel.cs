using Not.DomainUtils;
using Not.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Application.Models;

public class CoreCompetitionModel
{
    public static CoreCompetitionModel MapFrom(Competition competition)
    {
        return new CoreCompetitionModel
        {
            Name = competition.Name,
            Ruleset = competition.Ruleset,
            Type = competition.Type,
        };
    }

    public string Name { get; init; } = default!;
    public CompetitionType Type { get; init; }
    public CompetitionRuleset Ruleset { get; init; }

    public Competition MapToDomain()
    {
        return new Competition(Name, Ruleset, Type);
    }
}

public class CoreCombinationModel
{
    public static CoreCombinationModel MapFrom(Combination combination)
    {
        return new CoreCombinationModel
        {
            Id = combination.Id,
            Number = combination.Number,
            Distance = combination.Distance,
            MinAverageSpeed = combination.MinAverageSpeed,
            MaxAverageSpeed = combination.MaxAverageSpeed,
            Athlete = AthleteModel.MapFrom(combination.Athlete),
            Horse = HorseModel.MapFrom(combination.Horse),
        };
    }

    public int Id { get; init; }
    public int Number { get; init; }
    public string? Distance { get; init; }
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }
    public AthleteModel Athlete { get; init; } = default!;
    public HorseModel Horse { get; init; } = default!;

    public Combination MapToDomain()
    {
        var athlete = new Athlete(Athlete.MapToDomain());
        var horse = new Horse(Horse.MaptoDomain());
        var minSpeed = Speed.Create(MinAverageSpeed);
        var maxSpeed = Speed.Create(MaxAverageSpeed);
        return new Combination(
            DomainHelper.EnsureId(Id),
            Number,
            athlete,
            horse,
            athlete.Club,
            Distance!,
            minSpeed,
            maxSpeed
        );
    }
}

public class CorePhaseModel
{
    public static CorePhaseModel MapFrom(Phase phase)
    {
        return new CorePhaseModel
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

    public Phase MapToDomain()
    {
        return new Phase(
            DomainHelper.EnsureId(Id),
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
            IsRequiredInspectionCompulsory
        );
    }
}

public class TotalModel
{
    public static TotalModel Create(Total total)
    {
        return new TotalModel
        {
            LastArriveTime = total.FinishTime?.ToDateTimeOffset(),
            AverageSpeed = total.AverageSpeed.ToDouble(),
            Interval = total.Interval.ToTimeSpan(),
            RideInterval = total.RideInterval.ToTimeSpan(),
            RecoveryInterval = total.RecoveryInterval.ToTimeSpan(),
            RecoveryIntervalWithoutFinal = total.RecoveryIntervalWithoutFinal.ToTimeSpan(),
        };
    }

    public DateTimeOffset? LastArriveTime { get; init; }
    public double AverageSpeed { get; init; }
    public TimeSpan Interval { get; init; }
    public TimeSpan RideInterval { get; init; }
    public TimeSpan RecoveryInterval { get; init; }
    public TimeSpan RecoveryIntervalWithoutFinal { get; init; }
}

public class EliminatedModel
{
    public static EliminatedModel MapFrom(Eliminated eliminated)
    {
        if (eliminated is FailedToQualify failedToQualify)
        {
            return new EliminatedModel
            {
                Code = eliminated.Code,
                Reason = eliminated.Complement,
                FtqCodes = failedToQualify.FtqCodes.ToArray(),
            };
        }
        else if (eliminated is Disqualified disqualified)
        {
            return new EliminatedModel
            {
                Code = eliminated.Code,
                Reason = eliminated.Complement,
                DqCodes = disqualified.DqCodes.ToArray(),
            };
        }
        return new EliminatedModel { Code = eliminated.Code, Reason = eliminated.Complement };
    }

    public string Code { get; init; } = default!;
    public string? Reason { get; init; }
    public FailToQualifyCode[]? FtqCodes { get; init; }
    public DisqualifyCode[] DqCodes { get; init; } = default!;

    public Eliminated MapToDomain()
    {
        return Code switch // TODO refactor Eliminated to non-abstract and only FTQ as separate class
        {
            Eliminated.FAILED_TO_QUALIFY => new FailedToQualify(FtqCodes!, Reason),
            Eliminated.WITHDRAWN => new Withdrawn(),
            Eliminated.DISQUALIFIED => new Disqualified(DqCodes, Reason!),
            Eliminated.FINISHED_NOT_RANKED => new FinishedNotRanked(Reason!),
            Eliminated.RETIRED => new Retired(),
            _ => throw new NotImplementedException(),
        };
    }
}

public class CoreParticipationModel
{
    public static CoreParticipationModel MapFrom(Participation participation)
    {
        var total = participation.GetTotal();

        return new CoreParticipationModel
        {
            Id = participation.Id,
            Category = participation.Category,
            Competition = CoreCompetitionModel.MapFrom(participation.Competition),
            Combination = CoreCombinationModel.MapFrom(participation.Combination),
            Phases = participation.Phases.Select(CorePhaseModel.MapFrom).ToArray(),
            Total = total == null ? null : TotalModel.Create(total),
            Eliminated = participation.Eliminated == null ? null : EliminatedModel.MapFrom(participation.Eliminated),
        };
    }

    public int Id { get; init; }
    public ParticipationCategory Category { get; init; } = default!;
    public CoreCompetitionModel? Competition { get; init; }
    public CoreCombinationModel Combination { get; init; } = default!;
    public CorePhaseModel[] Phases { get; init; } = default!;
    public TotalModel? Total { get; init; }
    public EliminatedModel? Eliminated { get; init; }

    public Participation MapToDomain()
    {
        var competition = Competition!.MapToDomain();
        var combination = Combination.MapToDomain();
        var phases = Phases!.Select(x => x.MapToDomain());
        var eliminated = Eliminated?.MapToDomain();
        return new Participation(
            DomainHelper.EnsureId(Id),
            Category,
            competition,
            combination,
            new(phases),
            eliminated
        );
    }
}

public class RankingEntryModel
{
    public static RankingEntryModel MapFrom(RankingEntry rankingEntry)
    {
        return new RankingEntryModel
        {
            Participation = CoreParticipationModel.MapFrom(rankingEntry.Participation),
            Rank = rankingEntry.Rank,
            IsNotRanked = rankingEntry.IsNotRanked,
        };
    }

    public CoreParticipationModel Participation { get; init; } = default!;
    public int? Rank { get; init; }
    public bool IsNotRanked { get; init; }

    public RankingEntry MapToDomain()
    {
        var participation = Participation.MapToDomain();
        return new RankingEntry(DomainHelper.GenerateId(), participation, Rank, IsNotRanked);
    }
}

public class RanklistModel
{
    public static RanklistModel MapFrom(Ranklist ranklist)
    {
        return new RanklistModel
        {
            Name = ranklist.Name,
            Ruleset = ranklist.Ruleset,
            Type = ranklist.Type,
            Category = ranklist.Category,
            CompetitionFeiId = ranklist.Ranking.CompetitionFeiId,
            FeiRule = ranklist.Ranking.FeiRule,
            FeiScheduleNumber = ranklist.Ranking.FeiScheduleNumber,
            Entries = ranklist.Entries.Select(RankingEntryModel.MapFrom).ToArray(),
        };
    }

    public string Name { get; init; } = default!;
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public ParticipationCategory Category { get; init; }
    public string? CompetitionFeiId { get; init; }
    public string? FeiRule { get; init; }
    public string? FeiScheduleNumber { get; init; }
    public RankingEntryModel[] Entries { get; init; } = [];

    public Ranklist MapToDomain()
    {
        var entries = Entries.Select(x => x.MapToDomain());
        var competition = new Competition(Name, Ruleset, Type);
        var ranking = new Ranking(competition, Category, CompetitionFeiId, FeiRule, FeiScheduleNumber, entries);
        return new Ranklist(ranking, entries);
    }
}

public class ArchiveModel : IDocument
{
    public static ArchiveModel MapFrom(
        EnduranceEvent enduranceEvent,
        IEnumerable<Official> officials,
        IEnumerable<Ranklist> ranklists
    )
    {
        return new ArchiveModel
        {
            Id = enduranceEvent.Id,
            Country = CountryModel.MapFrom(enduranceEvent.PopulatedPlace.Country),
            City = enduranceEvent.PopulatedPlace.City,
            Location = enduranceEvent.PopulatedPlace.Location,
            FeiShowId = enduranceEvent.FeiShowId,
            FeiId = enduranceEvent.FeiId,
            FeiEventCode = enduranceEvent.FeiEventCode,
            StartDay = enduranceEvent.EventSpan.StartDay,
            EndDay = enduranceEvent.EventSpan.EndDay,
            Officials = officials.Select(OfficialModel.MapFrom).ToArray(),
            Ranklists = ranklists.Select(RanklistModel.MapFrom).ToArray(),
        };
    }

    public int Id { get; init; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public CountryModel Country { get; init; } = default!;
    public string City { get; init; } = default!;
    public string? Location { get; init; }
    public string? FeiShowId { get; init; }
    public string? FeiId { get; init; }
    public string? FeiEventCode { get; init; }
    public DateTimeOffset StartDay { get; init; }
    public DateTimeOffset EndDay { get; init; }
    public OfficialModel[] Officials { get; init; } = default!;
    public RanklistModel[] Ranklists { get; init; } = default!;

    public ArchiveEntry MapToDomain()
    {
        var enduranceEvent = new EnduranceEvent(
            Id,
            Country.MapToDomain(),
            City,
            Location ?? "",
            StartDay,
            EndDay,
            FeiShowId,
            FeiId,
            FeiEventCode
        );
        var officials = Officials.Select(x => x.MapToCoreDomain());
        var ranklists = Ranklists.Select(x => x.MapToDomain());
        return new ArchiveEntry(enduranceEvent, officials, ranklists);
    }
}
