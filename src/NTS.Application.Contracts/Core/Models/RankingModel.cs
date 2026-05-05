using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Application.Contracts.Core.Models;

public class RankingModel : IEventScoped, ISoftDeletableDocument, IKrudModel<Ranking>
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
