using System.Text.RegularExpressions;
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

/// <summary>
/// The search of accounts by name (#643, ADR-0012). There is no listing of accounts: a search takes at least three
/// characters, which it matches literally, finds at most ten accounts by name, and answers each with its id, its display
/// name and an email that is partly masked, never anything an account keeps for signing in. It looks among the members of
/// a Tenant, or, for the route that asks for it by name, among every account. Who may search at all, and how often, is the
/// policy's and the limiter's, and the routes ask them first.
/// </summary>
internal sealed class AccountSearch
{
    public const int MIN_SEARCH_LENGTH = 3;
    public const int MAX_MATCHES = 10;

    readonly IMongoCollection<BsonDocument> _users;

    public AccountSearch(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _users = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(options.Value.UsersCollection);
    }

    /// <param name="text">At least three characters, matched as they are written and in any case.</param>
    /// <param name="tenantId">The Tenant whose members are searched; none to search every account.</param>
    public async Task<IReadOnlyList<AccountMatch>> SearchAsync(
        string text,
        string? tenantId,
        CancellationToken cancellationToken
    )
    {
        var needle = text.Trim();
        if (needle.Length < MIN_SEARCH_LENGTH)
        {
            throw new ArgumentException($"A search is for at least {MIN_SEARCH_LENGTH} characters.", nameof(text));
        }

        var filter = Builders<BsonDocument>.Filter.Regex("Name", new BsonRegularExpression(Regex.Escape(needle), "i"));
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
            .Sort(new BsonDocument("Name", 1))
            .Limit(MAX_MATCHES)
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
