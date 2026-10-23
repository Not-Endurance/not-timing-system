using System.Reflection;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Shared;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// What the routes of a family of reference data need of its collection, whether the rows belong to a Tenant or to the
/// platform: a page of the rows that satisfy a filter, one row by its id, a row made, a row changed member by member,
/// and a row removed. A collection of a Tenant is <see cref="TypedTenantCollection{T}"/>, which can only find, change
/// and remove the Tenant's rows; the other is <see cref="GlobalCollection{T}"/>.
/// </summary>
internal interface IReferenceCollection<T>
    where T : class, IDocument
{
    Task<IReadOnlyList<T>> ReadAsync(
        ODataQueryOptions<T>? options,
        int skip,
        int take,
        CancellationToken cancellationToken
    );
    Task<T?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> InsertAsync(T row, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, BsonDocument update, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>A filter of the caller that is valid, and that the database could not run on a row of the list.</summary>
internal sealed class UnrunnableExpressionException : Exception
{
    public UnrunnableExpressionException(Exception cause)
        : base("The database could not run the filter on a row of the list.", cause) { }
}

/// <summary>How a page of the rows of a query is read, whoever the rows belong to.</summary>
internal static class ReferencePages
{
    /// <summary>
    /// A page of the rows of the query that satisfy the filter, in the order asked for, or in the order of their ids when
    /// none was, so that the pages of one list do not overlap.
    /// </summary>
    public static async Task<IReadOnlyList<T>> ReadAsync<T>(
        IQueryable<T> query,
        ODataQueryOptions<T>? options,
        int skip,
        int take,
        CancellationToken cancellationToken
    )
        where T : class, IDocument
    {
        return await ReadAsync(query, options, x => x.OrderBy(row => row.Id), null, skip, take, cancellationToken);
    }

    /// <summary>
    /// The same for rows that are not documents of a collection (a join of two): <paramref name="unsorted"/> is the order of
    /// the rows when the caller asked for none, and <paramref name="afterSort"/> what follows the sort of the caller, so that
    /// two rows that are equal in it come in the same order whatever the page.
    /// </summary>
    public static async Task<IReadOnlyList<T>> ReadAsync<T>(
        IQueryable<T> query,
        ODataQueryOptions<T>? options,
        Func<IQueryable<T>, IOrderedQueryable<T>> unsorted,
        Func<IOrderedQueryable<T>, IOrderedQueryable<T>>? afterSort,
        int skip,
        int take,
        CancellationToken cancellationToken
    )
        where T : class
    {
        async Task<IReadOnlyList<T>> Page()
        {
            var settings = new ODataQuerySettings();
            var filtered = options?.Filter != null ? (IQueryable<T>)options.Filter.ApplyTo(query, settings) : query;
            var ordered = unsorted(filtered);
            if (options?.OrderBy != null)
            {
                ordered = options.OrderBy.ApplyTo(filtered, settings);
                ordered = afterSort == null ? ordered : afterSort(ordered);
            }

            return await ordered.Skip(skip).Take(take).ToListAsync(cancellationToken);
        }

        try
        {
            return await Page();
        }
        catch (Exception ex) when (options?.Filter != null && IsOfTheExpression(ex))
        {
            throw new UnrunnableExpressionException(ex);
        }
    }

    /// <summary>
    /// Whether the failure of a read that applies a filter of the caller is the expression's, which is for the caller to
    /// correct, and not the database's (a sort is a list of members, which the database always runs). The driver reports an
    /// error that it meets while it evaluates a part of the expression that does not depend on a row (<c>1 div 0</c>) as a
    /// <see cref="TargetInvocationException"/>. A failure to connect, to authenticate or to answer in time is another kind of
    /// exception, but a command that the server refuses is always a <see cref="MongoCommandException"/>, whether the query is
    /// the reason or the state of the server is (not primary, shutting down, a time limit, rights), so the code tells: the
    /// query cannot be run when the code is a bad value, a type mismatch, a failed parse, an overflow or a failed conversion,
    /// or one of the numbered assertions of the expressions of the server, which it names <c>Location</c> and the number (a
    /// division by zero, a part of a text that starts past its end).
    /// </summary>
    public static bool IsOfTheExpression(Exception ex)
    {
        return ex switch
        {
            TargetInvocationException => true,
            MongoCommandException { Code: 2 or 9 or 14 or 15 or 241 } => true,
            MongoCommandException { CodeName: { } name } => name.StartsWith("Location", StringComparison.Ordinal),
            _ => false,
        };
    }
}

/// <summary>
/// The collections that belong to no Tenant: the platform's reference data, which every Tenant reads and the Developer
/// keeps. It is a list of one, the countries, and a name that is not on it cannot be opened this way.
/// </summary>
internal sealed class GlobalCollections
{
    const string COUNTRIES = "countries";

    readonly IMongoDatabase _database;

    public GlobalCollections(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _database = client.GetDatabase(options.Value.Database);
    }

    public static IReadOnlyList<string> Collections { get; } = [COUNTRIES];

    public GlobalCollection<T> Of<T>(string collection)
        where T : class, IDocument
    {
        if (!Collections.Contains(collection))
        {
            throw new ArgumentException(
                $"'{collection}' is not a collection that belongs to the platform.",
                nameof(collection)
            );
        }

        return new GlobalCollection<T>(_database.GetCollection<T>(collection));
    }
}

/// <summary>A collection of the platform's reference data, as the model that is stored in it.</summary>
internal sealed class GlobalCollection<T> : IReferenceCollection<T>
    where T : class, IDocument
{
    readonly IMongoCollection<T> _documents;

    public GlobalCollection(IMongoCollection<T> documents)
    {
        _documents = documents;
    }

    public async Task<IReadOnlyList<T>> ReadAsync(
        ODataQueryOptions<T>? options,
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        return await ReferencePages.ReadAsync(_documents.AsQueryable(), options, skip, take, cancellationToken);
    }

    public async Task<T?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _documents.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> InsertAsync(T row, CancellationToken cancellationToken)
    {
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

    public async Task<bool> UpdateAsync(Guid id, BsonDocument update, CancellationToken cancellationToken)
    {
        var result = await _documents.UpdateOneAsync(
            x => x.Id == id,
            new BsonDocumentUpdateDefinition<T>(update),
            cancellationToken: cancellationToken
        );
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _documents.DeleteOneAsync(x => x.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }
}
