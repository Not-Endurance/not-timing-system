using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NTS.Contracts.Shared;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The collections whose documents belong to a Tenant (ADR-0012, #643): every document carries the id of its Tenant in
/// <c>TenantId</c>, and every one of these collections is read and written through <see cref="TenantCollection"/> and
/// never directly. A collection that no Tenant owns is not in the list and cannot be opened that way: accounts, the
/// countries and the Tenants are global, the state a person keeps per Event is the person's own, and the pending
/// Snapshots go with the Functions API.
/// </summary>
internal static class TenantOwned
{
    public const string TENANT_ID = "TenantId";
    public const string CONFIGURE_EVENTS = "configure_events";
    public const string EVENT_INFORMATIONS = "event_informations";
    public const string EVENT_GRANTS = "event_grants";
    public const string EVENT_OFFICIALS = "event_officials";
    public const string EVENT_OPERATORS = "event_operators";
    public const string EVENT_PARTICIPATIONS = "event_participations";
    public const string EVENT_RANKINGS = "event_rankings";
    public const string EVENT_HANDOUTS = "event_handouts";
    public const string EVENT_SNAPSHOT_RESULTS = "event-snapshotResults";

    /// <summary>What an Event makes when it starts and keeps while it runs: its documents, each with the id of the Event.</summary>
    public static IReadOnlyList<string> OfAnEvent { get; } =
        [
            EVENT_OFFICIALS,
            EVENT_OPERATORS,
            EVENT_PARTICIPATIONS,
            EVENT_RANKINGS,
            EVENT_HANDOUTS,
            EVENT_SNAPSHOT_RESULTS,
        ];

    public static IReadOnlyList<string> Collections { get; } =
        [
            "athletes",
            "horses",
            "clubs",
            CONFIGURE_EVENTS,
            EVENT_INFORMATIONS,
            EVENT_OFFICIALS,
            EVENT_OPERATORS,
            EVENT_PARTICIPATIONS,
            EVENT_RANKINGS,
            EVENT_HANDOUTS,
            EVENT_SNAPSHOT_RESULTS,
            EVENT_GRANTS,
        ];
}

/// <summary>A write was asked for with no current Tenant to make it in: whoever asked had to refuse it before.</summary>
internal sealed class NoCurrentTenantException : InvalidOperationException
{
    public NoCurrentTenantException(string collection)
        : base($"A write to '{collection}' needs a current Tenant, and there is none.") { }
}

/// <summary>
/// Opens the collections a Tenant owns for one Tenant. Which Tenant is the caller's to say: inside an Event it is the
/// Event's, elsewhere the account's selected or home Tenant, and none for an account that has neither, and then nothing
/// is found and nothing is written. There is no way to open one for every Tenant: what reaches across Tenants is a
/// named capability of its own (<see cref="CrossTenantReads"/>), never a flag of this.
/// </summary>
internal sealed class TenantCollections
{
    readonly IMongoDatabase _database;

    public TenantCollections(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _database = client.GetDatabase(options.Value.Database);
    }

    public TenantCollection Of(string collection, string? currentTenantId)
    {
        RequireOwned(collection);
        return new TenantCollection(
            collection,
            string.IsNullOrWhiteSpace(currentTenantId) ? null : currentTenantId,
            _database.GetCollection<BsonDocument>(collection)
        );
    }

    /// <summary>The same collection, read and written as the model that is stored in it.</summary>
    public TypedTenantCollection<T> Of<T>(string collection, string? currentTenantId)
        where T : class, IDocument
    {
        RequireOwned(collection);
        return new TypedTenantCollection<T>(
            collection,
            string.IsNullOrWhiteSpace(currentTenantId) ? null : currentTenantId,
            _database.GetCollection<T>(collection)
        );
    }

    static void RequireOwned(string collection)
    {
        if (!TenantOwned.Collections.Contains(collection))
        {
            throw new ArgumentException($"'{collection}' is not a collection that a Tenant owns.", nameof(collection));
        }
    }
}

/// <summary>
/// One collection as one Tenant sees it: every filter is joined with the Tenant, so another Tenant's document is not found,
/// changed, replaced or deleted, and it cannot be reached by naming the other Tenant in the filter either. A document that
/// is written is stamped with the Tenant, whatever it says, and an update that names the Tenant is made to leave it as it
/// is (the Tenant's own value is set last, and any other operator on it is a conflict the database refuses), so nothing
/// moves from one Tenant to another. With no Tenant a read finds nothing and a write is refused.
/// </summary>
internal sealed class TenantCollection
{
    readonly IMongoCollection<BsonDocument> _documents;

    public TenantCollection(string name, string? tenantId, IMongoCollection<BsonDocument> documents)
    {
        Name = name;
        TenantId = tenantId;
        _documents = documents;
    }

    public string Name { get; }

    /// <summary>The Tenant it was opened for; none finds nothing.</summary>
    public string? TenantId { get; }

    public async Task<IReadOnlyList<BsonDocument>> FindAsync(
        FilterDefinition<BsonDocument>? filter,
        CancellationToken cancellationToken
    )
    {
        return TenantId == null ? [] : await _documents.Find(Scoped(filter)).ToListAsync(cancellationToken);
    }

    /// <summary>The ids of the Tenant's documents the filter finds, which is all that is read of them.</summary>
    public async Task<IReadOnlyList<Guid>> FindIdsAsync(
        FilterDefinition<BsonDocument>? filter,
        CancellationToken cancellationToken
    )
    {
        if (TenantId == null)
        {
            return [];
        }

        var found = await _documents
            .Find(Scoped(filter))
            .Project(new BsonDocument("_id", 1))
            .ToListAsync(cancellationToken);
        return [.. found.Select(x => BsonGuids.Of(x, "_id")).OfType<Guid>()];
    }

    public async Task<BsonDocument?> FindOneAsync(
        FilterDefinition<BsonDocument> filter,
        CancellationToken cancellationToken
    )
    {
        return TenantId == null ? null : await _documents.Find(Scoped(filter)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long> CountAsync(FilterDefinition<BsonDocument>? filter, CancellationToken cancellationToken)
    {
        return TenantId == null
            ? 0
            : await _documents.CountDocumentsAsync(Scoped(filter), cancellationToken: cancellationToken);
    }

    /// <summary>Inserts the document, stamped with the Tenant.</summary>
    public Task InsertAsync(BsonDocument document, CancellationToken cancellationToken)
    {
        var stamped = document.DeepClone().AsBsonDocument;
        stamped[TenantOwned.TENANT_ID] = Required();
        return _documents.InsertOneAsync(stamped, cancellationToken: cancellationToken);
    }

    /// <summary>Replaces the Tenant's document the filter finds, keeping it in the Tenant; false when it finds none.</summary>
    public async Task<bool> ReplaceAsync(
        FilterDefinition<BsonDocument> filter,
        BsonDocument replacement,
        CancellationToken cancellationToken
    )
    {
        var tenant = Required();
        var stamped = replacement.DeepClone().AsBsonDocument;
        stamped[TenantOwned.TENANT_ID] = tenant;
        var result = await _documents.ReplaceOneAsync(Scoped(filter), stamped, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    /// <summary>Changes the Tenant's document the filter finds; false when it finds none. It never makes one.</summary>
    public async Task<bool> UpdateAsync(
        FilterDefinition<BsonDocument> filter,
        UpdateDefinition<BsonDocument> update,
        CancellationToken cancellationToken
    )
    {
        var tenant = Required();
        var result = await _documents.UpdateOneAsync(
            Scoped(filter),
            Builders<BsonDocument>.Update.Combine(
                update,
                Builders<BsonDocument>.Update.Set(TenantOwned.TENANT_ID, tenant)
            ),
            cancellationToken: cancellationToken
        );
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken)
    {
        Required();
        var result = await _documents.DeleteOneAsync(Scoped(filter), cancellationToken);
        return result.DeletedCount > 0;
    }

    /// <summary>Inserts the documents, each stamped with the Tenant; the documents of an Event that starts are made so.</summary>
    public async Task InsertManyAsync(IReadOnlyCollection<BsonDocument> documents, CancellationToken cancellationToken)
    {
        var tenant = Required();
        if (documents.Count == 0)
        {
            return;
        }

        var stamped = documents
            .Select(x =>
            {
                var copy = x.DeepClone().AsBsonDocument;
                copy[TenantOwned.TENANT_ID] = tenant;
                return copy;
            })
            .ToList();
        await _documents.InsertManyAsync(stamped, cancellationToken: cancellationToken);
    }

    /// <summary>Removes every one of the Tenant's documents the filter finds; how many there were.</summary>
    public async Task<long> DeleteManyAsync(FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken)
    {
        Required();
        var result = await _documents.DeleteManyAsync(Scoped(filter), cancellationToken);
        return result.DeletedCount;
    }

    FilterDefinition<BsonDocument> Scoped(FilterDefinition<BsonDocument>? filter)
    {
        var ofTheTenant = Builders<BsonDocument>.Filter.Eq(TenantOwned.TENANT_ID, TenantId);
        return filter == null ? ofTheTenant : Builders<BsonDocument>.Filter.And(ofTheTenant, filter);
    }

    string Required()
    {
        return TenantId ?? throw new NoCurrentTenantException(Name);
    }
}
