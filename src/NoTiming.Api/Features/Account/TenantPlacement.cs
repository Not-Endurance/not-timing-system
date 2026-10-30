using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Places an account in a Tenant (#601, ADR-0002, ADR-0012). A Tenant is a document of the <c>tenants</c> collection
/// that is created on demand, the first time a person from its country is placed in it. An account has one home
/// Tenant and a memberships array that holds a membership in it, with no role. The home Tenant is set once: profile
/// edits never move it, so an account that has one is left as it is.
/// </summary>
internal sealed class TenantPlacement
{
    const string TENANTS = "tenants";
    const string HOME_TENANT = "HomeTenantId";
    const string MEMBERSHIPS = "Memberships";
    const string PROFILE_COUNTRY = "CountryRegion";

    readonly IMongoCollection<BsonDocument> _tenants;
    readonly IMongoCollection<BsonDocument> _users;
    readonly SelectableCountries _countries;
    readonly ILogger<TenantPlacement> _logger;

    public TenantPlacement(
        IMongoClient client,
        IOptions<NIdentityOptions> options,
        SelectableCountries countries,
        ILogger<TenantPlacement> logger
    )
    {
        var database = client.GetDatabase(options.Value.Database);
        _tenants = database.GetCollection<BsonDocument>(TENANTS);
        _users = database.GetCollection<BsonDocument>(options.Value.UsersCollection);
        _countries = countries;
        _logger = logger;
    }

    /// <summary>
    /// Makes sure the Tenant exists, and answers the fields of an account placed in it. Creating it twice, or from two
    /// requests at once, leaves one document: a name that is already there is not overwritten.
    /// </summary>
    public async Task<BsonDocument> PlaceInAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        await _tenants.UpdateOneAsync(
            new BsonDocument("_id", tenant.Id),
            Builders<BsonDocument>.Update.SetOnInsert("Name", tenant.Name).SetOnInsert("Kind", tenant.Kind),
            new UpdateOptions { IsUpsert = true },
            cancellationToken
        );
        return new BsonDocument
        {
            [HOME_TENANT] = tenant.Id,
            [MEMBERSHIPS] = new BsonArray
            {
                new BsonDocument { ["TenantId"] = Membership.In(tenant).TenantId, ["Roles"] = new BsonArray() },
            },
        };
    }

    /// <summary>
    /// Places an account that was there before Tenants, by the country of its profile and with the matching of the
    /// profile screen. One that has a Tenant is not touched, and one whose profile has no country, or one that matches
    /// none, stays unassigned until it picks a country. Placing is never a reason for a sign-in to fail.
    /// </summary>
    public async Task EnsureHomeTenantAsync(NIdentityUser user, CancellationToken cancellationToken = default)
    {
        if (user.OtherFields != null && user.OtherFields.Contains(HOME_TENANT))
        {
            return;
        }

        try
        {
            var profileCountry = user.TextOf(PROFILE_COUNTRY);
            if (profileCountry == null)
            {
                return;
            }

            var tenant = Tenant.DerivedFrom(profileCountry, await _countries.AllAsync(cancellationToken));
            if (tenant == null)
            {
                return;
            }

            var fields = await PlaceInAsync(tenant, cancellationToken);
            var unplaced = Builders<BsonDocument>.Filter.And(
                new BsonDocument("_id", new BsonBinaryData(user.Id, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Filter.Exists(HOME_TENANT, false)
            );
            await _users.UpdateOneAsync(
                unplaced,
                Builders<BsonDocument>
                    .Update.Set(HOME_TENANT, fields[HOME_TENANT])
                    .Set(MEMBERSHIPS, fields[MEMBERSHIPS]),
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex) when (ex is MongoException or InvalidOperationException)
        {
            _logger.LogError(
                AuthEvents.TENANT_PLACEMENT_FAILED,
                ex,
                "User {UserId} could not be placed in a Tenant.",
                user.Id
            );
        }
    }
}
