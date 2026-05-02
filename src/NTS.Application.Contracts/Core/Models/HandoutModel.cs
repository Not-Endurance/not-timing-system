using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.Core.Models;

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
