using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NTS.Contracts.Core.Models;

/// <summary>
/// A stored Handout: its id, its Event and the id of its Participation. The Participation id is a top-level field so
/// that the Handouts of a Participation are found by a filter the server applies (ADR-0006).
/// </summary>
public class HandoutModel : IDocument, IEventScoped, ISoftDeletableDocument, IKrudModel<Handout>
{
    public static HandoutModel From(Handout handout)
    {
        var model = new HandoutModel();
        model.MapFrom(handout);
        return model;
    }

    public Guid Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public Guid EventId { get; set; }
    public Guid ParticipationId { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    public void MapFrom(Handout handout)
    {
        Id = handout.Id;
        EventId = handout.EventId;
        ParticipationId = handout.ParticipationId;
    }

    public Handout MapToEntity()
    {
        return new Handout(EventId, ParticipationId, Id);
    }
}
