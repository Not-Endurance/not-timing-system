using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.OData.Query;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Shared;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// A collection of the reference data (Clubs, Horses, Athletes, the Setup of an Event) as one Tenant sees it, typed by
/// the model that is stored in it (#603, ADR-0012). It holds what <see cref="TenantCollection"/> holds for the documents
/// that are read without a model, and for the same reasons: every query is joined with the Tenant, so another Tenant's
/// row is not found, changed or deleted by an id or by a filter, a row is stamped with the Tenant when it is written
/// and never moves, and with no Tenant a read finds nothing and a write is refused. What it adds is the one thing a
/// typed collection can do, which is to apply an OData filter and sort to the rows that are the Tenant's.
/// </summary>
internal sealed class TypedTenantCollection<T> : IReferenceCollection<T>
    where T : class, IDocument
{
    static readonly PropertyInfo TENANT_ID = typeof(T).GetProperty(nameof(IDocument.TenantId))!;

    readonly IMongoCollection<T> _documents;

    public TypedTenantCollection(string name, string? tenantId, IMongoCollection<T> documents)
    {
        Name = name;
        TenantId = tenantId;
        _documents = documents;
    }

    public string Name { get; }

    /// <summary>The Tenant it was opened for; none finds nothing.</summary>
    public string? TenantId { get; }

    /// <summary>
    /// A page of the Tenant's rows that satisfy the filter, in the order asked for, or in the order of their ids when none
    /// was, so that the pages of one list do not overlap.
    /// </summary>
    public Task<IReadOnlyList<T>> ReadAsync(
        ODataQueryOptions<T>? options,
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        return ReadAsync(null, options, skip, take, cancellationToken);
    }

    /// <summary>The same, of the Tenant's rows that satisfy the predicate too: what a caller sees of the Tenant's rows.</summary>
    public async Task<IReadOnlyList<T>> ReadAsync(
        Expression<Func<T, bool>>? where,
        ODataQueryOptions<T>? options,
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        var tenant = TenantId;
        if (tenant == null)
        {
            return [];
        }

        IQueryable<T> query = _documents.AsQueryable().Where(x => x.TenantId == tenant);
        if (where != null)
        {
            query = query.Where(where);
        }

        return await ReferencePages.ReadAsync(query, options, skip, take, cancellationToken);
    }

    public async Task<T?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenant = TenantId;
        return tenant == null
            ? null
            : await _documents.Find(x => x.Id == id && x.TenantId == tenant).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Inserts the row stamped with the Tenant; false when a row with its id is already there, whoever's it is.</summary>
    public async Task<bool> InsertAsync(T row, CancellationToken cancellationToken)
    {
        TENANT_ID.SetValue(row, Required());
        try
        {
            await _documents.InsertOneAsync(row, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <summary>
    /// Applies the update to the Tenant's row with the id and keeps it in the Tenant; false when it is not the Tenant's or
    /// is not there. It never makes one.
    /// </summary>
    public async Task<bool> UpdateAsync(Guid id, BsonDocument update, CancellationToken cancellationToken)
    {
        var tenant = Required();
        var set = update.TryGetValue("$set", out var named) ? named.AsBsonDocument.DeepClone().AsBsonDocument : new();
        set[TenantOwned.TENANT_ID] = tenant;
        var combined = new BsonDocument("$set", set);
        if (update.TryGetValue("$unset", out var removed))
        {
            combined["$unset"] = removed;
        }

        var result = await _documents.UpdateOneAsync(
            x => x.Id == id && x.TenantId == tenant,
            new BsonDocumentUpdateDefinition<T>(combined),
            cancellationToken: cancellationToken
        );
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenant = Required();
        var result = await _documents.DeleteOneAsync(x => x.Id == id && x.TenantId == tenant, cancellationToken);
        return result.DeletedCount > 0;
    }

    string Required()
    {
        return TenantId ?? throw new NoCurrentTenantException(Name);
    }
}
