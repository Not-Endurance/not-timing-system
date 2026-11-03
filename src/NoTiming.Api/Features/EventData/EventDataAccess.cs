using MongoDB.Bson;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// What the writes of every family of what an Event keeps need (#604), in one place: the Tenant's collection a row is in,
/// which is the Tenant of its Event, whether another row still names it, and the notice to the viewers of the Event that a
/// Participation changed. Whether the caller may write is not asked here: the endpoint filter has decided it (#671).
/// </summary>
internal sealed class EventDataAccess
{
    readonly EventStore _events;
    readonly TenantCollections _tenants;
    readonly IParticipationChanges _changes;

    public EventDataAccess(EventStore events, TenantCollections tenants, IParticipationChanges changes)
    {
        _events = events;
        _tenants = tenants;
        _changes = changes;
    }

    /// <summary>
    /// The Event with the id and the stage it is in now, none when it is not there. The host's own jobs ask this, with
    /// nobody to ask the policy for, and see to it themselves that they touch only the stage they are for (#629, ADR-0007).
    /// </summary>
    public Task<EventFacts?> FindAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return _events.FindAsync(eventId, cancellationToken);
    }

    public TypedTenantCollection<TModel> Collection<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        EventFacts facts
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return _tenants.Of<TModel>(family.Collection, facts.TenantId);
    }

    /// <summary>
    /// Whether a row of another family of the Event still names the row: a Ranking that counts a Participation, a Handout
    /// that is of it. They are looked for in the Tenant of the Event, which is where they are.
    /// </summary>
    public async Task<bool> IsReferencedAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        TModel row,
        EventFacts facts,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        foreach (var reference in family.ReferencedBy)
        {
            var naming = new BsonDocument(reference.Member, BsonGuids.Binary(row.Id));
            if (await _tenants.Of(reference.Collection, facts.TenantId).CountAsync(naming, cancellationToken) > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tells the viewers of the Event that the row changed, when it is a row they follow; every write calls it once, after it was stored.</summary>
    public Task AnnounceAsync<TModel, TEntity>(EventDataFamily<TModel, TEntity> family, TModel row)
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return family.AnnouncesChanges ? _changes.AnnounceAsync(row.EventId, row.Id) : Task.CompletedTask;
    }
}
