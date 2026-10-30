using MongoDB.Bson;
using Not.Domain.Abstractions;
using Not.Identity;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Shared;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// What the writes of every family of what an Event keeps need (#604), in one place: the Event a row belongs to and the
/// policy's answer to whether the caller may change what it keeps, the Tenant's collection a row is in, which is the
/// Tenant of its Event, and the notice to the viewers of the Event that a Participation changed.
/// </summary>
internal sealed class EventDataAccess
{
    readonly EventStore _events;
    readonly TenantCollections _tenants;
    readonly CrossTenantReads _reads;
    readonly IParticipationChanges _changes;

    public EventDataAccess(
        EventStore events,
        TenantCollections tenants,
        CrossTenantReads reads,
        IParticipationChanges changes
    )
    {
        _events = events;
        _tenants = tenants;
        _reads = reads;
        _changes = changes;
    }

    /// <summary>
    /// The Event, when the caller may change what it keeps, and the answer that refuses it when not: 404 for an Event that
    /// is not there, and the policy's for a caller who may not, whether the Event has started or ended or the caller is
    /// not its Main Operator (ADR-0012).
    /// </summary>
    public async Task<(EventFacts? Event, IResult? Refusal)> OpenAsync(
        NIdentityUser user,
        Guid eventId,
        CancellationToken cancellationToken
    )
    {
        var facts = await _events.FindAsync(eventId, cancellationToken);
        if (facts is null)
        {
            return (null, JsonApiResults.NotFound());
        }

        var verdict = AccessPolicy.Decide(
            Capability.EditEventData,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        return verdict.IsAllowed ? (facts, null) : (null, AccessResults.Refused(verdict));
    }

    /// <summary>
    /// The Event, when the host may do the operation to it at the stage it is in (#629, ADR-0007), and none when it is not
    /// there or the operation is not let through to that stage. The host asks this where a person's write asks
    /// <see cref="OpenAsync"/>, so that what an Event that is no longer Live takes from the host is the one row of
    /// <see cref="HostPolicy"/>, and no route of the Api reaches it.
    /// </summary>
    public async Task<EventFacts?> OpenForHostAsync(
        HostOperation operation,
        Guid eventId,
        CancellationToken cancellationToken
    )
    {
        var facts = await _events.FindAsync(eventId, cancellationToken);
        return facts is not null && HostPolicy.IsAllowed(operation, facts.Stage) ? facts : null;
    }

    /// <summary>
    /// The row with the id, whichever Tenant it is in, which is how a row is found when only its id is known, as a reader
    /// finds it; and the Tenant it is in is then the Tenant of its Event, which is the one a write is made in.
    /// </summary>
    public Task<TModel?> FindAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        Guid id,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return _reads.FindEventRowAsync<TModel>(family.Collection, id, cancellationToken);
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
