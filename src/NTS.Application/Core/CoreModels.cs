using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Application.Shared;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace NTS.Application.Core;

public class ClubModel
{
    public static ClubModel MapFrom(Club club)
    {
        return new ClubModel { Id = club.Id, Name = club.Name };
    }

    public int Id { get; init; }
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; init; } = default!;

    public Club MapToEntity()
    {
        return new Club(Name, Id);
    }
}

public class OfficialModel : IEventScopedDocument, ISoftDeletableDocument, IKrudModel<Official>
{
    public static OfficialModel MapFrom(Official official)
    {
        var model = new OfficialModel();
        ((IKrudModel<Official>)model).MapFrom(official);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public string[] Names { get; set; } = [];
    public OfficialRole Role { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public Official MapToEntity()
    {
        return new Official(Names, Role, EventId, Id);
    }

    void IKrudModel<Official>.MapFrom(Official official)
    {
        Id = official.Id;
        EventId = official.EventId;
        Names = official.Person.Names;
        Role = official.Role;
    }
}

public class CompetitionModel
{
    public static CompetitionModel MapFrom(Competition competition)
    {
        return new CompetitionModel
        {
            Name = competition.Name,
            Ruleset = competition.Ruleset,
            Type = competition.Type,
        };
    }

    public string Name { get; init; } = default!;
    public CompetitionType Type { get; init; }
    public CompetitionRuleset Ruleset { get; init; }

    public Competition MapToEntity()
    {
        return new Competition(Name, Ruleset, Type);
    }
}

public class AthleteModel
{
    public static AthleteModel MapFrom(Athlete athlete)
    {
        return new AthleteModel
        {
            Id = athlete.Id,
            FeiId = athlete.FeiId,
            Names = athlete.Names.Names,
            Country = CountryModel.From(athlete.Country),
            Club = athlete.Club == null ? null : ClubModel.MapFrom(athlete.Club),
        };
    }

    public int Id { get; init; }
    public string[] Names { get; init; } = default!;
    public CountryModel Country { get; init; } = default!;
    public ClubModel? Club { get; init; }
    public string? FeiId { get; init; }

    public Athlete MapToEntity()
    {
        var country = Country.MapToEntity();
        var club = Club?.MapToEntity();
        return new Athlete(Names, country, club, FeiId, Id);
    }
}

public class HorseModel
{
    public static HorseModel MapFrom(Horse horse)
    {
        return new HorseModel
        {
            Id = horse.Id,
            FeiId = horse.FeiId,
            Name = horse.Name,
        };
    }

    public int Id { get; init; }
    public string? FeiId { get; init; }
    public string Name { get; init; } = default!;

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

    public Combination MapToEntity()
    {
        var athlete = Athlete.MapToEntity();
        var horse = Horse.MapToEntity();
        return new Combination(Number, athlete, horse, athlete.Club, Distance!, MinAverageSpeed, MaxAverageSpeed, Id);
    }
}

public class PhaseModel
{
    public static PhaseModel MapFrom(Phase phase)
    {
        return new PhaseModel
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

    public Phase MapToEntity()
    {
        return new Phase(
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
            IsRequiredInspectionCompulsory,
            Id
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

    public Eliminated MapToEntity()
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

public class ParticipationModel : IEventScopedDocument, ISoftDeletableDocument, IKrudModel<Participation>
{
    public static ParticipationModel MapFrom(Participation participation)
    {
        var total = participation.GetTotal();

        return new ParticipationModel
        {
            Id = participation.Id,
            EventId = participation.EventId,
            Category = participation.Category,
            Competition = CompetitionModel.MapFrom(participation.Competition),
            Combination = CombinationModel.MapFrom(participation.Combination),
            Phases = participation.Phases.Select(PhaseModel.MapFrom).ToArray(),
            Total = total == null ? null : TotalModel.Create(total),
            Eliminated = participation.Eliminated == null ? null : EliminatedModel.MapFrom(participation.Eliminated),
        };
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public ParticipationCategory Category { get; set; } = default!;
    public CompetitionModel Competition { get; set; } = default!;
    public CombinationModel Combination { get; set; } = default!;
    public PhaseModel[] Phases { get; set; } = default!;
    public TotalModel? Total { get; set; }
    public EliminatedModel? Eliminated { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public Participation MapToEntity()
    {
        var competition = Competition.MapToEntity();
        var combination = Combination.MapToEntity();
        var phases = Phases!.Select(x => x.MapToEntity());
        var eliminated = Eliminated?.MapToEntity();
        return new Participation(Category, competition, combination, new(phases), eliminated, EventId, Id);
    }

    void IKrudModel<Participation>.MapFrom(Participation participation)
    {
        var total = participation.GetTotal();

        Id = participation.Id;
        EventId = participation.EventId;
        Category = participation.Category;
        Competition = CompetitionModel.MapFrom(participation.Competition);
        Combination = CombinationModel.MapFrom(participation.Combination);
        Phases = participation.Phases.Select(PhaseModel.MapFrom).ToArray();
        Total = total == null ? null : TotalModel.Create(total);
        Eliminated = participation.Eliminated == null ? null : EliminatedModel.MapFrom(participation.Eliminated);
    }
}

public class RankingEntryModel
{
    public static RankingEntryModel MapFrom(RankingEntry rankingEntry)
    {
        return new RankingEntryModel
        {
            Participation = ParticipationModel.MapFrom(rankingEntry.Participation),
            Rank = rankingEntry.Rank,
            IsNotRanked = rankingEntry.IsNotRanked,
        };
    }

    public ParticipationModel Participation { get; init; } = default!;
    public int? Rank { get; init; }
    public bool IsNotRanked { get; init; }

    public RankingEntry MapToEntity()
    {
        var participation = Participation.MapToEntity();
        return new RankingEntry(participation, Rank, IsNotRanked);
    }
}

public class RanklistModel
{
    public static RanklistModel MapFrom(Ranklist ranklist)
    {
        return new RanklistModel
        {
            Id = ranklist.RankingId,
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

    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public ParticipationCategory Category { get; init; }
    public string? CompetitionFeiId { get; init; }
    public string? FeiRule { get; init; }
    public string? FeiScheduleNumber { get; init; }
    public RankingEntryModel[] Entries { get; init; } = [];

    public Ranklist MapToEntity()
    {
        var entries = Entries.Select(x => x.MapToEntity()).ToList();
        var competition = new Competition(Name, Ruleset, Type);
        var eventId = Entries.FirstOrDefault()?.Participation.EventId ?? Id;
        var ranking = new Ranking(
            Name,
            Ruleset,
            Type,
            Category,
            CompetitionFeiId,
            FeiRule,
            FeiScheduleNumber,
            entries,
            eventId,
            Id
        );
        return new Ranklist(ranking, entries);
    }
}

public class EnduranceEventModel : IIdentifiable, IMongoIdentityDocument, ISoftDeletableDocument, IKrudModel<EnduranceEvent>
{
    public static EnduranceEventModel From(EnduranceEvent enduranceEvent)
    {
        var model = new EnduranceEventModel();
        model.MapFrom(enduranceEvent);
        return model;
    }

    [JsonIgnore]
    [BsonId]
    public string MongoId { get; set; } = "";
    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public CountryModel Country { get; set; } = default!;
    public string City { get; set; } = default!;
    public string? Location { get; set; }
    public string? FeiShowId { get; set; }
    public string? FeiId { get; set; }
    public string? FeiEventCode { get; set; }
    public DateTimeOffset StartDay { get; set; }
    public DateTimeOffset EndDay { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(EnduranceEvent enduranceEvent)
    {
        Id = enduranceEvent.Id;
        Country = CountryModel.From(enduranceEvent.PopulatedPlace.Country);
        City = enduranceEvent.PopulatedPlace.City;
        Location = enduranceEvent.PopulatedPlace.Location;
        FeiShowId = enduranceEvent.FeiShowId;
        FeiId = enduranceEvent.FeiId;
        FeiEventCode = enduranceEvent.FeiEventCode;
        StartDay = enduranceEvent.EventSpan.StartDay;
        EndDay = enduranceEvent.EventSpan.EndDay;
    }

    public EnduranceEvent MapToEntity()
    {
        var country = Country.MapToEntity();
        var place = new PopulatedPlace(country, City, Location);
        var span = new EventSpan(StartDay, EndDay);
        return new EnduranceEvent(place, span, FeiShowId, FeiId, FeiEventCode, Id);
    }
}

public class RankingModel : IEventScopedDocument, ISoftDeletableDocument, IKrudModel<Ranking>
{
    public static RankingModel From(Ranking ranking)
    {
        var model = new RankingModel();
        model.MapFrom(ranking);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public string Name { get; set; } = default!;
    public CompetitionRuleset Ruleset { get; set; }
    public CompetitionType Type { get; set; }
    public ParticipationCategory Category { get; set; }
    public string? CompetitionFeiId { get; set; }
    public string? FeiRule { get; set; }
    public string? FeiScheduleNumber { get; set; }
    public RankingEntryModel[] Entries { get; set; } = [];
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(Ranking ranking)
    {
        Id = ranking.Id;
        EventId = ranking.EventId;
        Name = ranking.Name;
        Ruleset = ranking.Ruleset;
        Type = ranking.Type;
        Category = ranking.Category;
        CompetitionFeiId = ranking.CompetitionFeiId;
        FeiRule = ranking.FeiRule;
        FeiScheduleNumber = ranking.FeiScheduleNumber;
        Entries = ranking.Entries.Select(RankingEntryModel.MapFrom).ToArray();
    }

    public Ranking MapToEntity()
    {
        var entries = Entries.Select(x => x.MapToEntity()).ToList();
        return new Ranking(
            Name,
            Ruleset,
            Type,
            Category,
            CompetitionFeiId,
            FeiRule,
            FeiScheduleNumber,
            entries,
            EventId,
            Id
        );
    }
}

public class HandoutModel : IEventScopedDocument, ISoftDeletableDocument, IKrudModel<Handout>
{
    public static HandoutModel From(Handout handout)
    {
        var model = new HandoutModel();
        model.MapFrom(handout);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public ParticipationModel Participation { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(Handout handout)
    {
        Id = handout.Id;
        EventId = handout.EventId;
        Participation = ParticipationModel.MapFrom(handout.Participation);
    }

    public Handout MapToEntity()
    {
        return new Handout(Participation.MapToEntity(), Id);
    }
}

public class CoreSnapshotModel
{
    public static CoreSnapshotModel MapFrom(Snapshot snapshot)
    {
        return new CoreSnapshotModel
        {
            Number = snapshot.Number,
            Type = snapshot.Type,
            Method = snapshot.Method,
            RecordedAt = snapshot.Timestamp.ToDateTimeOffset(),
        };
    }

    public int Number { get; set; }
    public SnapshotType Type { get; set; }
    public SnapshotMethod Method { get; set; }
    public DateTimeOffset RecordedAt { get; set; }

    public Snapshot MapToEntity()
    {
        return new Snapshot(Number, Type, Method, new Timestamp(RecordedAt));
    }
}

public class SnapshotResultModel : IEventScopedDocument, ISoftDeletableDocument, IKrudModel<SnapshotResult>
{
    public static SnapshotResultModel From(SnapshotResult result)
    {
        var model = new SnapshotResultModel();
        model.MapFrom(result);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public CoreSnapshotModel Snapshot { get; set; } = default!;
    public SnapshotResultType Type { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(SnapshotResult result)
    {
        Id = result.Id;
        EventId = result.EventId;
        Snapshot = CoreSnapshotModel.MapFrom(result.Snapshot);
        Type = result.Type;
    }

    public SnapshotResult MapToEntity()
    {
        return new SnapshotResult(Snapshot.MapToEntity(), Type, EventId, Id);
    }
}

public class ArchiveEntryModel : IDocument, IKrudModel<ArchiveEntry>
{
    public static ArchiveEntryModel From(
        EnduranceEvent enduranceEvent,
        IEnumerable<Official> officials,
        IEnumerable<Ranklist> ranklists
    )
    {
        var model = new ArchiveEntryModel();

        return new ArchiveEntryModel
        {
            Id = enduranceEvent.Id,
            Country = CountryModel.From(enduranceEvent.PopulatedPlace.Country),
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

    public int Id { get; set; } = default!;
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public CountryModel Country { get; set; } = default!;
    public string City { get; set; } = default!;
    public string? Location { get; set; }
    public string? FeiShowId { get; set; }
    public string? FeiId { get; set; }
    public string? FeiEventCode { get; set; }
    public DateTimeOffset StartDay { get; set; }
    public DateTimeOffset EndDay { get; set; }
    public OfficialModel[] Officials { get; set; } = default!;
    public RanklistModel[] Ranklists { get; set; } = default!;

    public void MapFrom(ArchiveEntry archiveEntry)
    {
        Id = archiveEntry.EnduranceEvent.Id;
        Country = CountryModel.From(archiveEntry.EnduranceEvent.PopulatedPlace.Country);
        City = archiveEntry.EnduranceEvent.PopulatedPlace.City;
        Location = archiveEntry.EnduranceEvent.PopulatedPlace.Location;
        FeiShowId = archiveEntry.EnduranceEvent.FeiShowId;
        FeiId = archiveEntry.EnduranceEvent.FeiId;
        FeiEventCode = archiveEntry.EnduranceEvent.FeiEventCode;
        StartDay = archiveEntry.EnduranceEvent.EventSpan.StartDay;
        EndDay = archiveEntry.EnduranceEvent.EventSpan.EndDay;
        Officials = archiveEntry.Officials.Select(OfficialModel.MapFrom).ToArray();
        Ranklists = archiveEntry.Ranklists.Select(RanklistModel.MapFrom).ToArray();
    }

    public ArchiveEntry MapToEntity()
    {
        var country = Country.MapToEntity();
        var place = new PopulatedPlace(country, City, Location ?? "");
        var span = new EventSpan(StartDay, EndDay);
        var enduranceEvent = new EnduranceEvent(place, span, FeiShowId, FeiId, FeiEventCode, Id);
        var officials = Officials.Select(x => x.MapToEntity());
        var ranklists = Ranklists.Select(x => x.MapToEntity());
        return new ArchiveEntry(enduranceEvent, officials, ranklists);
    }
}
