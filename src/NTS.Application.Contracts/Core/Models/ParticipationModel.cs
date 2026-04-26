using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;


namespace NTS.Application.Contracts.Core.Models;

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


