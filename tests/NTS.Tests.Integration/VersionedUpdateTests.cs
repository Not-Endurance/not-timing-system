using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// A document that counts its writes is changed against the version it was read at, in one conditional update that also
/// makes the version one more (#604, ADR-0013). It is tested here on the Participations, over MongoDB, in the store and not
/// through the routes: a request reads the row before it writes it, so the check it makes on what it read is not what keeps
/// a second writer out, and only writers that meet in the database show that the write itself is conditional.
/// </summary>
public sealed class VersionedUpdateTests : IClassFixture<MongoFixture>
{
    const string PARTICIPATIONS = "event_participations";

    readonly MongoFixture _mongo;

    public VersionedUpdateTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task A_change_at_the_version_the_row_is_at_is_applied_and_makes_the_version_one_more()
    {
        var tenant = NewTenant();
        var id = await SeedAsync(tenant, version: 3);

        var applied = await Store(tenant).UpdateAtVersionAsync(id, SetCategory("Junior"), 3, default);

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, PARTICIPATIONS, id))!;
        Assert.True(applied);
        Assert.Equal(4, stored["Version"].AsInt32);
        Assert.Equal("Junior", stored["Category"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(0)]
    public async Task A_change_at_any_other_version_is_not_applied_and_changes_nothing(int version)
    {
        var tenant = NewTenant();
        var id = await SeedAsync(tenant, version: 3);
        var before = await RegistrySeed.StoredAsync(_mongo.ConnectionString, PARTICIPATIONS, id);

        var applied = await Store(tenant).UpdateAtVersionAsync(id, SetCategory("Junior"), version, default);

        Assert.False(applied);
        Assert.Equal(before, await RegistrySeed.StoredAsync(_mongo.ConnectionString, PARTICIPATIONS, id));
    }

    [Fact]
    public async Task A_row_that_no_write_has_counted_is_at_version_0_and_the_first_change_makes_it_1()
    {
        var tenant = NewTenant();
        var id = await SeedAsync(tenant, version: 0);
        var store = Store(tenant);

        var first = await store.UpdateAtVersionAsync(id, SetCategory("Junior"), 0, default);
        var again = await store.UpdateAtVersionAsync(id, SetCategory("Youth"), 0, default);
        var next = await store.UpdateAtVersionAsync(id, SetCategory("Senior"), 1, default);

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, PARTICIPATIONS, id))!;
        Assert.True(first);
        Assert.False(again);
        Assert.True(next);
        Assert.Equal(2, stored["Version"].AsInt32);
        Assert.Equal("Senior", stored["Category"].AsString);
    }

    [Fact]
    public async Task Of_many_changes_made_at_one_version_only_one_is_applied()
    {
        var tenant = NewTenant();
        var id = await SeedAsync(tenant, version: 1);
        var store = Store(tenant);

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 12)
                .Select(x => store.UpdateAtVersionAsync(id, SetCategory($"Category {x}"), 1, default))
        );

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, PARTICIPATIONS, id))!;
        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(2, stored["Version"].AsInt32);
    }

    [Fact]
    public async Task A_change_does_not_reach_the_row_of_another_Tenant_or_a_row_that_is_not_there()
    {
        var (tenant, other) = (NewTenant(), NewTenant());
        var id = await SeedAsync(tenant, version: 3);
        var before = await RegistrySeed.StoredAsync(_mongo.ConnectionString, PARTICIPATIONS, id);

        var asOther = await Store(other).UpdateAtVersionAsync(id, SetCategory("Junior"), 3, default);
        var missing = await Store(tenant).UpdateAtVersionAsync(Guid.NewGuid(), SetCategory("Junior"), 0, default);

        Assert.False(asOther);
        Assert.False(missing);
        Assert.Equal(before, await RegistrySeed.StoredAsync(_mongo.ConnectionString, PARTICIPATIONS, id));
    }

    [Fact]
    public async Task A_document_that_does_not_count_its_writes_cannot_be_changed_that_way()
    {
        var tenant = NewTenant();
        var club = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Mine");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Store(tenant, "clubs").UpdateAtVersionAsync(club, new BsonDocument(), 0, default)
        );
    }

    TypedTenantCollection<NTS.Contracts.Core.Models.ParticipationModel> Store(string tenant)
    {
        return Tenants().Of<NTS.Contracts.Core.Models.ParticipationModel>(PARTICIPATIONS, tenant);
    }

    TypedTenantCollection<NTS.Contracts.Setup.Models.ClubModel> Store(string tenant, string clubs)
    {
        return Tenants().Of<NTS.Contracts.Setup.Models.ClubModel>(clubs, tenant);
    }

    TenantCollections Tenants()
    {
        return new TenantCollections(
            new MongoClient(_mongo.ConnectionString),
            Options.Create(new NIdentityOptions { Database = UserSeed.DATABASE })
        );
    }

    async Task<Guid> SeedAsync(string tenant, int version)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(Guid.NewGuid(), 1, Guid.NewGuid());
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation, version);
        return participation.Id;
    }

    static BsonDocument SetCategory(string category)
    {
        return new BsonDocument("$set", new BsonDocument("Category", category));
    }

    static string NewTenant()
    {
        return $"country-{Guid.NewGuid():N}"[..16];
    }
}
