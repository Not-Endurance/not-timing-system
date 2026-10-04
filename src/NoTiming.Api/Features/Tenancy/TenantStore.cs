using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NTS.Domain.Aggregates;
using NTS.Domain.Objects;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The Tenants as documents of the <c>tenants</c> collection (#601, ADR-0012): a key of its kind, a name, and the rules
/// of its Regional competitions. A Tenant is made when the first person of its country registers, and it is operational
/// once an account holds the Tenant Root role in it, which only the Developer's command gives. Rules are edited here
/// member by member, so two edits of different members that cross do not undo each other; a document that has none has
/// the FEI's. The shape of the rules is that of the Core Event's copy of them, because the Event copies them when it
/// starts.
/// </summary>
internal sealed class TenantStore
{
    const string TENANTS = "tenants";
    const string RULES = "RegionalRules";
    const string ONLY_AVERAGE_LOOP_SPEED = "OnlyAverageLoopSpeed";
    const string RANKER_CODE = "RankerCode";

    readonly IMongoCollection<BsonDocument> _tenants;
    readonly IMongoCollection<BsonDocument> _users;

    public TenantStore(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        var database = client.GetDatabase(options.Value.Database);
        _tenants = database.GetCollection<BsonDocument>(TENANTS);
        _users = database.GetCollection<BsonDocument>(options.Value.UsersCollection);
    }

    public async Task<Tenant?> FindAsync(string id, CancellationToken cancellationToken)
    {
        var document = await _tenants.Find(new BsonDocument("_id", id)).FirstOrDefaultAsync(cancellationToken);
        return document == null ? null : Read(document);
    }

    /// <summary>
    /// Whether the Tenant can hold Events: an account is a Tenant Root of it. It is read from the accounts, not kept on the
    /// Tenant, so there is nothing to fall out of step with the roles.
    /// </summary>
    public async Task<bool> IsOperationalAsync(string id, CancellationToken cancellationToken)
    {
        var roots = await _users.CountDocumentsAsync(
            new BsonDocument(
                AccountRoles.MEMBERSHIPS,
                new BsonDocument(
                    "$elemMatch",
                    new BsonDocument { { "TenantId", id }, { "Roles", Membership.TENANT_ROOT } }
                )
            ),
            new CountOptions { Limit = 1 },
            cancellationToken
        );
        return roots > 0;
    }

    /// <summary>
    /// Saves the members of the rules that are not what the edit started from, each by itself, and nothing else of the
    /// Tenant: a member that the edit leaves as it was is not written, so an edit of one cannot undo a crossing edit of
    /// another, and what else a document keeps among its rules is left as it is. False when there was nothing to write.
    /// </summary>
    public async Task<bool> SetRulesAsync(
        string id,
        RegionalRules before,
        RegionalRules after,
        CancellationToken cancellationToken
    )
    {
        var updates = new List<UpdateDefinition<BsonDocument>>();
        if (after.OnlyAverageLoopSpeed != before.OnlyAverageLoopSpeed)
        {
            updates.Add(
                Builders<BsonDocument>.Update.Set(RULES + "." + ONLY_AVERAGE_LOOP_SPEED, after.OnlyAverageLoopSpeed)
            );
        }

        if (after.RankerCode != before.RankerCode)
        {
            updates.Add(
                after.RankerCode == null
                    ? Builders<BsonDocument>.Update.Unset(RULES + "." + RANKER_CODE)
                    : Builders<BsonDocument>.Update.Set(RULES + "." + RANKER_CODE, after.RankerCode)
            );
        }

        if (updates.Count == 0)
        {
            return false;
        }

        await _tenants.UpdateOneAsync(
            new BsonDocument("_id", id),
            Builders<BsonDocument>.Update.Combine(updates),
            cancellationToken: cancellationToken
        );
        return true;
    }

    static Tenant? Read(BsonDocument document)
    {
        if (
            !document.TryGetValue("_id", out var id)
            || !id.IsString
            || !document.TryGetValue("Name", out var name)
            || !name.IsString
            || !document.TryGetValue("Kind", out var kind)
            || !kind.IsString
        )
        {
            return null;
        }

        return new Tenant(id.AsString, name.AsString, kind.AsString, RulesOf(document));
    }

    static RegionalRules RulesOf(BsonDocument tenant)
    {
        if (!tenant.TryGetValue(RULES, out var value) || !value.IsBsonDocument)
        {
            return RegionalRules.None;
        }

        var rules = value.AsBsonDocument;
        var onlyAverageLoopSpeed =
            rules.TryGetValue(ONLY_AVERAGE_LOOP_SPEED, out var flag) && flag.IsBoolean && flag.AsBoolean;
        var code = rules.TryGetValue(RANKER_CODE, out var ranker) && ranker.IsString ? ranker.AsString : null;
        try
        {
            return new RegionalRules(onlyAverageLoopSpeed, code);
        }
        catch (ArgumentException)
        {
            // A code that is not valid is as if there were none: the document is not the Api's to refuse reading.
            return new RegionalRules(onlyAverageLoopSpeed);
        }
    }
}
