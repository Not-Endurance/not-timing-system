using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>An account found by name: who it is and an email that is partly masked, which tells two of one name apart.</summary>
internal sealed class AccountMatch
{
    public AccountMatch(Guid id, string displayName, string maskedEmail)
    {
        Id = id;
        DisplayName = displayName;
        MaskedEmail = maskedEmail;
    }

    public Guid Id { get; }
    public string DisplayName { get; }
    public string MaskedEmail { get; }
}

/// <summary>An account named by its id: who it is, as the people who work with it know it.</summary>
internal sealed class AccountName
{
    public AccountName(Guid id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public Guid Id { get; }
    public string DisplayName { get; }
}

/// <summary>
/// The search of accounts by name (#643, ADR-0012). There is no listing of accounts: a search takes words, one of at least
/// three characters, which it matches literally and in the database (<see cref="NameSearch"/>), finds a page of accounts by
/// name, and answers each with its id, its display name and an email that is partly masked, never anything an account keeps for
/// signing in. It looks among the members of a Tenant, or, for the route that asks for it by name, among every account. Who may
/// search at all, and how often, is the policy's and the limiter's, and the routes ask them first.
/// </summary>
internal sealed class AccountSearch
{
    readonly IMongoCollection<BsonDocument> _users;

    public AccountSearch(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _users = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(options.Value.UsersCollection);
    }

    /// <summary>
    /// A page of the display names of the accounts with these ids, in the order of the names (the other way round when it is
    /// asked) and then of the ids, so that none is twice or missing between two pages; and whether another page follows. It is
    /// for a route that has already decided that the caller may be told who they are (the people who recorded the times of an
    /// Event, #619). An id that is no account, and an account with no name, is left out.
    /// </summary>
    public async Task<(IReadOnlyList<AccountName> Names, bool HasMore)> NamesOfAsync(
        IReadOnlyCollection<Guid> ids,
        bool descending,
        int skip,
        int size,
        CancellationToken cancellationToken
    )
    {
        if (ids.Count == 0)
        {
            return ([], false);
        }

        var rows = await _users
            .Find(
                Builders<BsonDocument>.Filter.In("_id", ids.Select(BsonGuids.Binary))
                    & Builders<BsonDocument>.Filter.Type("Name", BsonType.String)
            )
            .Project(new BsonDocument { { "_id", 1 }, { "Name", 1 } })
            .Sort(new BsonDocument { { "Name", descending ? -1 : 1 }, { "_id", 1 } })
            .Skip(skip)
            .Limit(size + 1)
            .ToListAsync(cancellationToken);
        return (
            [
                .. rows.Take(size)
                    .Where(x => BsonGuids.Of(x, "_id") != null)
                    .Select(x => new AccountName(BsonGuids.Of(x, "_id")!.Value, x["Name"].AsString)),
            ],
            rows.Count > size
        );
    }

    /// <param name="search">The words to look for, matched as they are written and in any case, and the page that is wanted.</param>
    /// <param name="tenantId">The Tenant whose members are searched; none to search every account.</param>
    public async Task<IReadOnlyList<AccountMatch>> SearchAsync(
        NameSearch search,
        string? tenantId,
        CancellationToken cancellationToken
    )
    {
        search.EnsureEnough();
        var filter = search.Matching("Name");
        if (tenantId != null)
        {
            filter &= new BsonDocument(
                AccountRoles.MEMBERSHIPS,
                new BsonDocument("$elemMatch", new BsonDocument("TenantId", tenantId))
            );
        }

        var rows = await _users
            .Find(filter)
            .Project(
                new BsonDocument
                {
                    { "_id", 1 },
                    { "Name", 1 },
                    { "Email", 1 },
                }
            )
            .Sort(search.OrderBy("Name", "_id"))
            .Skip(search.Skip)
            .Limit(search.Take)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Where(x => BsonGuids.Of(x, "_id") != null && x.TryGetValue("Name", out var name) && name.IsString)
                .Select(x => new AccountMatch(
                    BsonGuids.Of(x, "_id")!.Value,
                    x["Name"].AsString,
                    EmailMask.Of(x.TryGetValue("Email", out var email) && email.IsString ? email.AsString : null)
                )),
        ];
    }
}
