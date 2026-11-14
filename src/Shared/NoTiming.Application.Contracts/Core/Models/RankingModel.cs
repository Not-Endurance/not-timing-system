using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Enums;

namespace NoTiming.Application.Contracts.Core.Models;

public class RankingModel : IEventScoped, ISoftDeletableDocument, IKrudModel<Ranking>
{
    public static RankingModel From(Ranking ranking)
    {
        var model = new RankingModel();
        model.MapFrom(ranking);
        return model;
    }

    public int DomainVersion { get; set; }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public int EventId { get; set; }
    public string Name { get; set; } = default!;
    public bool EnglishNames { get; set; }
    public ParticipationCategory Category { get; set; }
    public string? FeiEventId { get; set; }
    public string? FeiEventCode { get; set; }
    public string? FeiCompetitionId { get; set; }
    public string? FeiRule { get; set; }
    public string? FeiScheduleNumber { get; set; }
    public RankingEntryModel[] Entries { get; set; } = [];
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(Ranking ranking)
    {
        DomainVersion = ranking.DomainVersion;
        Id = ranking.Id;
        EventId = ranking.EventId;
        Name = ranking.Name;
        EnglishNames = ranking.EnglishNames;
        Category = ranking.Category;
        FeiEventId = ranking.FeiEventId;
        FeiEventCode = ranking.FeiEventCode;
        FeiCompetitionId = ranking.FeiCompetitionId;
        FeiRule = ranking.FeiRule;
        FeiScheduleNumber = ranking.FeiScheduleNumber;
        Entries = ranking.Entries.Select(RankingEntryModel.MapFrom).ToArray();
    }

    public Ranking MapToEntity()
    {
        var entries = Entries.Select(x => x.MapToEntity()).ToList();
        return new Ranking(
            Name,
            EnglishNames,
            Category,
            FeiEventId,
            FeiEventCode,
            FeiCompetitionId,
            FeiRule,
            FeiScheduleNumber,
            entries,
            EventId,
            Id,
            storedDomainVersion: DomainVersion
        );
    }
}
