using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Setup.Models;
using NTS.Contracts.Shared.Models;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The rows of the reference data are read and written as the model that is stored in them, through the typed collection
/// of a Tenant (#603, ADR-0012), which keeps what the collection of documents keeps (<c>TenantScopedStoreTests</c>) for the
/// same reasons: every query is joined with the Tenant, so another Tenant's row is not found, changed or removed by an id,
/// a row is stamped with the Tenant when it is written and never moves, and with no Tenant a read finds nothing and a
/// write is refused. They are tested here on the Clubs, over MongoDB.
/// </summary>
public sealed class TypedTenantCollectionTests : IClassFixture<MongoFixture>
{
    const string CLUBS = "clubs";

    readonly MongoFixture _mongo;

    public TypedTenantCollectionTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Tenant_reads_only_its_own_rows_in_the_order_of_their_ids_and_a_Tenant_with_none_reads_nothing()
    {
        var (a, b) = (NewTenant(), NewTenant());
        var first = await RegistrySeed.ClubAsync(_mongo.ConnectionString, a, "Mine");
        var second = await RegistrySeed.ClubAsync(_mongo.ConnectionString, a, "Also mine");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, b, "Theirs");

        var asA = await Store().Of<ClubModel>(CLUBS, a).ReadAsync(null, 0, 100, default);
        var asNoOne = await Store().Of<ClubModel>(CLUBS, null).ReadAsync(null, 0, 100, default);
        var asBlank = await Store().Of<ClubModel>(CLUBS, "  ").ReadAsync(null, 0, 100, default);
        var asNew = await Store().Of<ClubModel>(CLUBS, NewTenant()).ReadAsync(null, 0, 100, default);

        Assert.Equal(new[] { first, second }.Order(), asA.Select(x => x.Id));
        Assert.All(asA, x => Assert.Equal(a, x.TenantId));
        Assert.Empty(asNoOne);
        Assert.Empty(asBlank);
        Assert.Empty(asNew);
    }

    [Fact]
    public async Task A_page_is_a_part_of_the_rows_of_the_Tenant_that_does_not_overlap_the_next_one()
    {
        var tenant = NewTenant();
        await RegistrySeed.ClubsAsync(_mongo.ConnectionString, tenant, 7, "Club");
        var asTenant = Store().Of<ClubModel>(CLUBS, tenant);

        var first = await asTenant.ReadAsync(null, 0, 3, default);
        var second = await asTenant.ReadAsync(null, 3, 3, default);
        var last = await asTenant.ReadAsync(null, 6, 3, default);

        Assert.Equal(new[] { 3, 3, 1 }, new[] { first.Count, second.Count, last.Count });
        Assert.Equal(7, first.Concat(second).Concat(last).Select(x => x.Id).Distinct().Count());
        Assert.Equal(
            first.Concat(second).Concat(last).Select(x => x.Id).Order(),
            first.Concat(second).Concat(last).Select(x => x.Id)
        );
    }

    [Fact]
    public async Task A_row_of_another_Tenant_is_not_found_changed_removed_or_made_again_by_its_id()
    {
        var (a, b) = (NewTenant(), NewTenant());
        var mine = await RegistrySeed.ClubAsync(_mongo.ConnectionString, a, "Mine");
        var theirs = await RegistrySeed.ClubAsync(_mongo.ConnectionString, b, "Theirs");
        var asA = Store().Of<ClubModel>(CLUBS, a);
        var theirsBefore = await RegistrySeed.StoredAsync(_mongo.ConnectionString, CLUBS, theirs);

        var found = await asA.FindAsync(theirs, default);
        var updated = await asA.UpdateAsync(
            theirs,
            new BsonDocument("$set", new BsonDocument("Name", "taken")),
            default
        );
        var removed = await asA.DeleteAsync(theirs, default);
        var made = await asA.InsertAsync(new ClubModel { Id = theirs, Name = "Stolen" }, default);

        Assert.Equal("Mine", (await asA.FindAsync(mine, default))!.Name);
        Assert.Null(found);
        Assert.False(updated);
        Assert.False(removed);
        Assert.False(made);
        Assert.Equal(theirsBefore, await RegistrySeed.StoredAsync(_mongo.ConnectionString, CLUBS, theirs));
    }

    [Fact]
    public async Task A_row_is_stamped_with_the_Tenant_it_is_made_in_and_no_update_moves_it()
    {
        var (a, b) = (NewTenant(), NewTenant());
        var asA = Store().Of<ClubModel>(CLUBS, a);
        var id = Guid.NewGuid();

        var made = await asA.InsertAsync(
            new ClubModel
            {
                Id = id,
                Name = "Made",
                TenantId = b,
            },
            default
        );
        await Assert.ThrowsAnyAsync<MongoException>(
            () => asA.UpdateAsync(id, new BsonDocument("$unset", new BsonDocument("TenantId", 1)), default)
        );
        var changed = await asA.UpdateAsync(
            id,
            new BsonDocument("$set", new BsonDocument { { "Name", "Moved?" }, { "TenantId", b } }),
            default
        );

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, CLUBS, id))!;
        Assert.True(made);
        Assert.True(changed);
        Assert.Equal(a, stored["TenantId"].AsString);
        Assert.Equal("Moved?", stored["Name"].AsString);
        Assert.Empty(await Store().Of<ClubModel>(CLUBS, b).ReadAsync(null, 0, 100, default));
    }

    [Fact]
    public async Task A_Tenant_changes_and_removes_its_own_rows()
    {
        var tenant = NewTenant();
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Before");
        var asTenant = Store().Of<ClubModel>(CLUBS, tenant);

        var changed = await asTenant.UpdateAsync(
            id,
            new BsonDocument("$set", new BsonDocument("Name", "After")),
            default
        );
        var read = await asTenant.FindAsync(id, default);
        var removed = await asTenant.DeleteAsync(id, default);
        var again = await asTenant.DeleteAsync(id, default);

        Assert.True(changed);
        Assert.Equal("After", read!.Name);
        Assert.True(removed);
        Assert.False(again);
        Assert.Null(await asTenant.FindAsync(id, default));
    }

    [Fact]
    public async Task With_no_current_Tenant_a_row_is_not_found_and_a_write_is_refused_and_writes_nothing()
    {
        var tenant = NewTenant();
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Kept");
        var before = await RegistrySeed.StoredAsync(_mongo.ConnectionString, CLUBS, id);
        var none = Store().Of<ClubModel>(CLUBS, null);

        Assert.Null(await none.FindAsync(id, default));
        await Assert.ThrowsAsync<NoCurrentTenantException>(
            () => none.InsertAsync(new ClubModel { Id = Guid.NewGuid(), Name = "x" }, default)
        );
        await Assert.ThrowsAsync<NoCurrentTenantException>(
            () => none.UpdateAsync(id, new BsonDocument("$set", new BsonDocument("Name", "x")), default)
        );
        await Assert.ThrowsAsync<NoCurrentTenantException>(() => none.DeleteAsync(id, default));
        Assert.Equal(before, await RegistrySeed.StoredAsync(_mongo.ConnectionString, CLUBS, id));
    }

    [Theory]
    [InlineData("users")]
    [InlineData("tenants")]
    [InlineData("countries")]
    [InlineData("something_else")]
    public void A_collection_that_no_Tenant_owns_is_not_opened_as_one(string collection)
    {
        Assert.Throws<ArgumentException>(() => Store().Of<ClubModel>(collection, "country-bg"));
    }

    [Fact]
    public async Task The_countries_are_the_only_collection_of_the_platform_and_belong_to_every_Tenant()
    {
        var id = await CountrySeed.AddAsync(_mongo.ConnectionString, "Globe", CountrySeed.UniqueIsoCode());
        var globals = new GlobalCollections(
            new MongoClient(_mongo.ConnectionString),
            Options.Create(new NIdentityOptions { Database = UserSeed.DATABASE })
        );

        var countries = globals.Of<CountryModel>("countries");

        Assert.Equal("Globe", (await countries.FindAsync(id, default))!.Name);
        Assert.Contains(id, (await countries.ReadAsync(null, 0, 500, default)).Select(x => x.Id));
        Assert.Throws<ArgumentException>(() => globals.Of<ClubModel>("clubs"));
        Assert.Throws<ArgumentException>(() => globals.Of<ClubModel>("users"));
    }

    TenantCollections Store()
    {
        return new TenantCollections(
            new MongoClient(_mongo.ConnectionString),
            Options.Create(new NIdentityOptions { Database = UserSeed.DATABASE })
        );
    }

    static string NewTenant()
    {
        return $"country-{Guid.NewGuid():N}"[..16];
    }
}
