using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NoTiming.Api.Features.Events;

/// <summary>An Event already has as many grants as it may: the Officials and Operators of an Event are some dozens.</summary>
internal sealed class GrantLimitReachedException : InvalidOperationException
{
    public GrantLimitReachedException(int limit)
        : base($"An Event has at most {limit} grants.") { }
}

/// <summary>
/// The grants of an Event (ADR-0012, #643), in the <c>event_grants</c> collection and always in the Tenant of the Event, so
/// a grant cannot be read or changed from another Tenant. Linking is idempotent: the same person to the same place is the
/// grant that was made, and a unique index says it for the requests that race. A grant that is pending, because its email
/// had no account, is attached by <see cref="GrantInvitations"/> when the person registers.
/// </summary>
internal sealed class EventGrantStore
{
    public const int MAX_GRANTS_PER_EVENT = 500;

    readonly TenantCollections _tenants;
    readonly CrossTenantReads _reads;

    public EventGrantStore(TenantCollections tenants, CrossTenantReads reads)
    {
        _tenants = tenants;
        _reads = reads;
    }

    /// <summary>
    /// The grant of the person to the place, whether it was made now and whether an account took it now. The same email in
    /// the same kind and role gives the first grant again, and an account that has appeared since a pending grant was made
    /// takes it.
    /// </summary>
    public async Task<(EventGrant Grant, bool Created, bool Attached)> LinkAsync(
        EventRecord record,
        GrantKind kind,
        OfficialRole? role,
        string email,
        Guid? accountId,
        CancellationToken cancellationToken
    )
    {
        var grants = _tenants.Of(TenantOwned.EVENT_GRANTS, record.TenantId);
        var normalized = EventGrant.NormalizeEmail(email);
        var existing = await FindExistingAsync(grants, record, kind, role, normalized, cancellationToken);
        if (existing == null)
        {
            // A guard against a flood, not a quota: a request that races this one can pass it by a few.
            var held = await grants.CountAsync(
                new BsonDocument(GrantDocuments.EVENT_ID, BsonGuids.Binary(record.Id)),
                cancellationToken
            );
            if (held >= MAX_GRANTS_PER_EVENT)
            {
                throw new GrantLimitReachedException(MAX_GRANTS_PER_EVENT);
            }

            var made =
                kind == GrantKind.Operator
                    ? EventGrant.ForOperator(Guid.NewGuid(), record.Id, record.TenantId, normalized, accountId)
                    : EventGrant.ForOfficial(
                        Guid.NewGuid(),
                        record.Id,
                        record.TenantId,
                        role!.Value,
                        normalized,
                        accountId
                    );
            try
            {
                await grants.InsertAsync(GrantDocuments.ToDocument(made), cancellationToken);
                return (made, true, false);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // Another request made it first: it is that grant that the person has.
                existing = await FindExistingAsync(grants, record, kind, role, normalized, cancellationToken);
            }
        }

        var attached = false;
        if (existing is { IsPending: true } && accountId is { } account)
        {
            await grants.UpdateAsync(
                new BsonDocument("_id", BsonGuids.Binary(existing.Id)),
                Builders<BsonDocument>.Update.Set(GrantDocuments.ACCOUNT_ID, BsonGuids.Binary(account)),
                cancellationToken
            );
            existing = existing.AttachedTo(account);
            attached = true;
        }

        return (
            existing ?? throw new InvalidOperationException("The grant is neither there nor made."),
            false,
            attached
        );
    }

    public async Task<IReadOnlyList<EventGrant>> ListAsync(EventRecord record, CancellationToken cancellationToken)
    {
        var documents = await _tenants
            .Of(TenantOwned.EVENT_GRANTS, record.TenantId)
            .FindAsync(new BsonDocument(GrantDocuments.EVENT_ID, BsonGuids.Binary(record.Id)), cancellationToken);
        return [.. documents.Select(GrantDocuments.ToGrant).OfType<EventGrant>().OrderBy(x => x.Email)];
    }

    /// <summary>What the account has been granted on the Event: the grants that are its own, and nothing from a pending one.</summary>
    public async Task<CallerGrants> GrantsOfAsync(EventRecord record, Guid account, CancellationToken cancellationToken)
    {
        var documents = await _tenants
            .Of(TenantOwned.EVENT_GRANTS, record.TenantId)
            .FindAsync(
                new BsonDocument
                {
                    { GrantDocuments.EVENT_ID, BsonGuids.Binary(record.Id) },
                    { GrantDocuments.ACCOUNT_ID, BsonGuids.Binary(account) },
                },
                cancellationToken
            );
        return CallerGrants.Of(documents.Select(GrantDocuments.ToGrant).OfType<EventGrant>());
    }

    /// <summary>The grant with the id, wherever it is, to learn the Event it belongs to. None when there is none.</summary>
    public async Task<EventGrant?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _reads.FindGrantAsync(id, cancellationToken);
        return document == null ? null : GrantDocuments.ToGrant(document);
    }

    /// <summary>Removes every grant of the Event, which the Event's own deletion does; how many there were.</summary>
    public Task<long> RemoveAllOfAsync(EventRecord record, CancellationToken cancellationToken)
    {
        return _tenants
            .Of(TenantOwned.EVENT_GRANTS, record.TenantId)
            .DeleteManyAsync(new BsonDocument(GrantDocuments.EVENT_ID, BsonGuids.Binary(record.Id)), cancellationToken);
    }

    public Task<bool> RemoveAsync(EventGrant grant, CancellationToken cancellationToken)
    {
        return _tenants
            .Of(TenantOwned.EVENT_GRANTS, grant.TenantId)
            .DeleteAsync(new BsonDocument("_id", BsonGuids.Binary(grant.Id)), cancellationToken);
    }

    static async Task<EventGrant?> FindExistingAsync(
        TenantCollection grants,
        EventRecord record,
        GrantKind kind,
        OfficialRole? role,
        string email,
        CancellationToken cancellationToken
    )
    {
        var filter = new BsonDocument
        {
            { GrantDocuments.EVENT_ID, BsonGuids.Binary(record.Id) },
            { GrantDocuments.KIND, kind.ToString() },
            { GrantDocuments.OFFICIAL_ROLE, role == null ? BsonNull.Value : role.Value.ToString() },
            { GrantDocuments.EMAIL, email },
        };
        var document = await grants.FindOneAsync(filter, cancellationToken);
        return document == null ? null : GrantDocuments.ToGrant(document);
    }
}

/// <summary>
/// Attaches the invitations that wait for an email to the account that has it (ADR-0012, #643). A grant to an email that
/// had no account is pending, and it is the account that registers with that address, and proves it with the code, that
/// takes it. It is the one write that reaches across Tenants by design, because a person registers once and is invited
/// from several, and it takes nothing but the email and the account of a person who has just proved the address.
/// </summary>
internal sealed class GrantInvitations
{
    readonly IMongoCollection<BsonDocument> _grants;
    readonly TenancyLog _log;

    public GrantInvitations(IMongoClient client, IOptions<NIdentityOptions> options, TenancyLog log)
    {
        _grants = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(TenantOwned.EVENT_GRANTS);
        _log = log;
    }

    /// <summary>
    /// Gives every pending grant to the address to the account: a grant is pending while it has no account, whether the
    /// field is missing or null. It is how many it attached that is returned. Attaching is never a reason for a sign-in to
    /// fail: when the grants cannot be written it is logged, nothing is attached, and the next sign-in tries again.
    /// </summary>
    public async Task<long> AttachAsync(string email, Guid account, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _grants.UpdateManyAsync(
                new BsonDocument
                {
                    { GrantDocuments.EMAIL, EventGrant.NormalizeEmail(email) },
                    { GrantDocuments.ACCOUNT_ID, BsonNull.Value },
                },
                Builders<BsonDocument>.Update.Set(GrantDocuments.ACCOUNT_ID, BsonGuids.Binary(account)),
                cancellationToken: cancellationToken
            );
            if (result.ModifiedCount > 0)
            {
                _log.InvitationsAttached(account, result.ModifiedCount);
            }

            return result.ModifiedCount;
        }
        catch (MongoException ex)
        {
            _log.InvitationsNotAttached(account, ex);
            return 0;
        }
    }
}
