using System.Reflection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;
using NTS.Nexus.HTTP.Mongo;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// Every document that belongs to a Tenant is read and written through its collection, which finds only the current
/// Tenant's documents (ADR-0012, #643): the Event's Tenant inside an Event, the account's selected or home Tenant
/// elsewhere, and with no current Tenant nothing at all. The tests seed two Tenants in each collection that a Tenant
/// owns and read them as each, as none, and as a Tenant that holds nothing there. Each test uses Tenants of its own, so
/// that the database can be shared by the tests of the class.
/// </summary>
public sealed class TenantScopedStoreTests : IClassFixture<MongoFixture>
{
    // The collections the Functions API keeps that no Tenant owns, and why: accounts, countries and Tenants are global;
    // the state a person keeps per Event is the person's own; pending Snapshots go with the Functions API (#611).
    static readonly string[] NOT_OWNED_BY_A_TENANT =
    [
        "users",
        "countries",
        "event_user_sessions",
        "event_pending_snapshots",
    ];

    public static TheoryData<string> OwnedCollections()
    {
        var rows = new TheoryData<string>();
        foreach (var collection in TenantOwned.Collections)
        {
            rows.Add(collection);
        }

        return rows;
    }

    readonly MongoFixture _mongo;

    public TenantScopedStoreTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public void The_registry_names_every_collection_the_Functions_API_keeps_that_a_Tenant_owns()
    {
        var kept = typeof(MongoConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.Name.EndsWith("_COLLECTION", StringComparison.Ordinal))
            .Select(x => (string)x.GetRawConstantValue()!)
            .Where(x => x != MongoConstants.NTS_DATABASE);

        var owned = kept.Where(x => !NOT_OWNED_BY_A_TENANT.Contains(x));

        Assert.Empty(owned.Except(TenantOwned.Collections));
    }

    [Fact]
    public void The_registry_keeps_the_global_collections_out_so_that_nobody_filters_an_account_by_a_Tenant()
    {
        Assert.Empty(TenantOwned.Collections.Intersect(NOT_OWNED_BY_A_TENANT));
        Assert.DoesNotContain("tenants", TenantOwned.Collections);
        Assert.Contains("event_grants", TenantOwned.Collections);
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task A_Tenant_reads_only_its_own_documents_in_every_collection_it_owns(string collection)
    {
        var (a, b) = await SeedTwoTenantsAsync(collection);

        var asA = await Store().Of(collection, a.Tenant).FindAsync(null, default);
        var asB = await Store().Of(collection, b.Tenant).FindAsync(null, default);

        Assert.Equal(a.Ids.Order(), asA.Select(IdOf).Order());
        Assert.Equal(b.Ids.Order(), asB.Select(IdOf).Order());
        Assert.All(asA, x => Assert.Equal(a.Tenant, x["TenantId"].AsString));
        Assert.All(asB, x => Assert.Equal(b.Tenant, x["TenantId"].AsString));
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task A_query_with_no_current_Tenant_returns_nothing_from_a_collection_that_holds_documents(
        string collection
    )
    {
        var (a, _) = await SeedTwoTenantsAsync(collection);
        await InsertAsync(collection, new BsonDocument("Marker", "unmarked"));
        await InsertAsync(collection, new BsonDocument { { "TenantId", "" }, { "Marker", "blank" } });
        await InsertAsync(collection, new BsonDocument { { "TenantId", BsonNull.Value }, { "Marker", "null" } });

        foreach (var none in new string?[] { null, "", "   " })
        {
            var store = Store().Of(collection, none);

            var found = await store.FindAsync(null, default);
            var one = await store.FindOneAsync(new BsonDocument("_id", a.Ids[0]), default);
            var anyOne = await store.FindOneAsync(new BsonDocument(), default);
            var count = await store.CountAsync(null, default);

            Assert.Empty(found);
            Assert.Null(one);
            Assert.Null(anyOne);
            Assert.Equal(0, count);
        }
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task A_Tenant_does_not_find_the_document_of_another_by_its_id_or_by_naming_the_other_Tenant(
        string collection
    )
    {
        var (a, b) = await SeedTwoTenantsAsync(collection);
        var asA = Store().Of(collection, a.Tenant);

        var byId = await asA.FindOneAsync(new BsonDocument("_id", b.Ids[0]), default);
        var byNamingTheOther = await asA.FindAsync(new BsonDocument("TenantId", b.Tenant), default);
        var count = await asA.CountAsync(new BsonDocument("TenantId", b.Tenant), default);

        Assert.Null(byId);
        Assert.Empty(byNamingTheOther);
        Assert.Equal(0, count);
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task A_Tenant_that_holds_nothing_in_a_collection_finds_nothing_in_it(string collection)
    {
        await SeedTwoTenantsAsync(collection);

        var found = await Store().Of(collection, NewTenant()).FindAsync(null, default);

        Assert.Empty(found);
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task A_document_of_the_constant_Tenant_or_of_none_is_found_by_no_Tenant_of_the_Api(string collection)
    {
        var tenant = NewTenant();
        var legacy = await InsertAsync(collection, new BsonDocument { { "TenantId", "nts" }, { "Marker", "legacy" } });
        var unmarked = await InsertAsync(collection, new BsonDocument { { "Marker", "unmarked" } });

        var found = await Store().Of(collection, tenant).FindAsync(null, default);

        Assert.Empty(found);
        Assert.NotEqual(legacy, unmarked);
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task Inserting_stamps_the_current_Tenant_whatever_the_document_says(string collection)
    {
        var tenant = NewTenant();
        var other = NewTenant();
        var asTenant = Store().Of(collection, tenant);
        var id = Guid.NewGuid();

        await asTenant.InsertAsync(
            new BsonDocument
            {
                { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
                { "TenantId", other },
                { "Marker", "inserted" },
            },
            default
        );

        var stored = await Collection(collection)
            .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
            .FirstAsync();
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Single(await asTenant.FindAsync(null, default));
        Assert.Empty(await Store().Of(collection, other).FindAsync(null, default));
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task Writing_with_no_current_Tenant_is_refused_and_writes_nothing(string collection)
    {
        var (a, _) = await SeedTwoTenantsAsync(collection);
        var none = Store().Of(collection, null);
        var before = await Collection(collection).CountDocumentsAsync(new BsonDocument());

        await Assert.ThrowsAsync<NoCurrentTenantException>(
            () => none.InsertAsync(new BsonDocument("Marker", "x"), default)
        );
        await Assert.ThrowsAsync<NoCurrentTenantException>(
            () => none.ReplaceAsync(new BsonDocument("_id", a.Ids[0]), new BsonDocument("Marker", "x"), default)
        );
        await Assert.ThrowsAsync<NoCurrentTenantException>(
            () =>
                none.UpdateAsync(
                    new BsonDocument("_id", a.Ids[0]),
                    Builders<BsonDocument>.Update.Set("Marker", "x"),
                    default
                )
        );
        await Assert.ThrowsAsync<NoCurrentTenantException>(
            () => none.DeleteAsync(new BsonDocument("_id", a.Ids[0]), default)
        );

        Assert.Equal(before, await Collection(collection).CountDocumentsAsync(new BsonDocument()));
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task A_Tenant_changes_replaces_and_deletes_its_own_documents_and_never_another_Tenants(
        string collection
    )
    {
        var (a, b) = await SeedTwoTenantsAsync(collection);
        var asA = Store().Of(collection, a.Tenant);
        var theirs = new BsonDocument("_id", b.Ids[0]);
        var mine = new BsonDocument("_id", a.Ids[0]);
        var theirsBefore = await Collection(collection).Find(theirs).FirstAsync();

        var updatedTheirs = await asA.UpdateAsync(
            theirs,
            Builders<BsonDocument>.Update.Set("Marker", "taken"),
            default
        );
        var replacedTheirs = await asA.ReplaceAsync(theirs, new BsonDocument("Marker", "taken"), default);
        var deletedTheirs = await asA.DeleteAsync(theirs, default);
        var updatedMine = await asA.UpdateAsync(mine, Builders<BsonDocument>.Update.Set("Marker", "mine"), default);
        var replacedMine = await asA.ReplaceAsync(
            new BsonDocument("_id", a.Ids[1]),
            new BsonDocument { { "Marker", "replaced" }, { "TenantId", b.Tenant } },
            default
        );
        var deletedMine = await asA.DeleteAsync(new BsonDocument("_id", a.Ids[1]), default);

        Assert.False(updatedTheirs);
        Assert.False(replacedTheirs);
        Assert.False(deletedTheirs);
        Assert.Equal(theirsBefore, await Collection(collection).Find(theirs).FirstAsync());
        Assert.True(updatedMine);
        Assert.Equal("mine", (await Collection(collection).Find(mine).FirstAsync())["Marker"].AsString);
        Assert.True(replacedMine);
        Assert.True(deletedMine);
        Assert.Equal(0, await Collection(collection).CountDocumentsAsync(new BsonDocument("_id", a.Ids[1])));
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task A_replacement_keeps_the_document_in_the_Tenant_it_was_in_whatever_it_says(string collection)
    {
        var (a, b) = await SeedTwoTenantsAsync(collection);
        var asA = Store().Of(collection, a.Tenant);

        await asA.ReplaceAsync(
            new BsonDocument("_id", a.Ids[0]),
            new BsonDocument { { "Marker", "replaced" }, { "TenantId", b.Tenant } },
            default
        );

        var stored = await Collection(collection).Find(new BsonDocument("_id", a.Ids[0])).FirstAsync();
        Assert.Equal(a.Tenant, stored["TenantId"].AsString);
        Assert.Equal("replaced", stored["Marker"].AsString);
    }

    [Theory]
    [MemberData(nameof(OwnedCollections))]
    public async Task An_update_cannot_move_a_document_to_another_Tenant(string collection)
    {
        var (a, b) = await SeedTwoTenantsAsync(collection);
        var asA = Store().Of(collection, a.Tenant);

        await asA.UpdateAsync(
            new BsonDocument("_id", a.Ids[0]),
            Builders<BsonDocument>.Update.Set("TenantId", b.Tenant).Set("Marker", "moved?"),
            default
        );

        var stored = await Collection(collection).Find(new BsonDocument("_id", a.Ids[0])).FirstAsync();
        Assert.Equal(a.Tenant, stored["TenantId"].AsString);
        Assert.Empty(await Store().Of(collection, b.Tenant).FindAsync(new BsonDocument("Marker", "moved?"), default));
    }

    [Theory]
    [InlineData("users")]
    [InlineData("tenants")]
    [InlineData("countries")]
    [InlineData("event_user_sessions")]
    [InlineData("something_else")]
    public void A_collection_that_no_Tenant_owns_is_not_opened_as_one(string collection)
    {
        Assert.Throws<ArgumentException>(() => Store().Of(collection, "country-bg"));
    }

    TenantCollections Store()
    {
        return new TenantCollections(
            new MongoClient(_mongo.ConnectionString),
            Options.Create(new NIdentityOptions { Database = UserSeed.DATABASE })
        );
    }

    IMongoCollection<BsonDocument> Collection(string name)
    {
        return new MongoClient(_mongo.ConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>(name);
    }

    static string NewTenant()
    {
        return $"country-{Guid.NewGuid():N}"[..16];
    }

    static BsonValue IdOf(BsonDocument document)
    {
        return document["_id"];
    }

    async Task<BsonValue> InsertAsync(string collection, BsonDocument document)
    {
        var id = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        document["_id"] = id;
        await Collection(collection).InsertOneAsync(document);
        return id;
    }

    /// <summary>Two documents of each of two Tenants, written as the Functions API would: straight into the collection.</summary>
    async Task<(Seeded A, Seeded B)> SeedTwoTenantsAsync(string collection)
    {
        var a = await SeedAsync(collection, NewTenant());
        var b = await SeedAsync(collection, NewTenant());
        return (a, b);
    }

    async Task<Seeded> SeedAsync(string collection, string tenant)
    {
        var ids = new List<BsonValue>();
        for (var i = 0; i < 2; i++)
        {
            ids.Add(
                await InsertAsync(
                    collection,
                    new BsonDocument { { "TenantId", tenant }, { "Marker", $"{tenant}-{i}" } }
                )
            );
        }

        return new Seeded(tenant, ids);
    }

    sealed class Seeded
    {
        public Seeded(string tenant, List<BsonValue> ids)
        {
            Tenant = tenant;
            Ids = ids;
        }

        public string Tenant { get; }
        public List<BsonValue> Ids { get; }
    }
}
