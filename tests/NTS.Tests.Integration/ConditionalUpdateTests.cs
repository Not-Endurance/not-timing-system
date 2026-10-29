using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.EventData;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// A row is changed only while it is as the writer expects it to be, in one update that also makes the check (#640): that is
/// what lets two hosts finalise the Rankings of the same Event at the same time and have one of them write. It is tested
/// here on the Rankings, over MongoDB, in the store and not through the finalisation: only writers that meet in the
/// database show that the check and the write are one operation. The condition of the finalisation is tested with it: the
/// Ranking none of whose entries holds a rank.
/// </summary>
public sealed class ConditionalUpdateTests : IClassFixture<MongoFixture>
{
    const string RANKINGS = "event_rankings";

    readonly MongoFixture _mongo;

    public ConditionalUpdateTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task A_change_to_a_row_that_is_as_the_condition_says_is_applied_and_keeps_it_in_the_Tenant()
    {
        var tenant = NewTenant();
        var id = await SeedAsync(
            tenant,
            new RankingEntry(Guid.NewGuid(), false),
            new RankingEntry(Guid.NewGuid(), true)
        );

        var applied = await Store(tenant)
            .UpdateWhenAsync(id, RankingFinaliser.NoEntryPlaced(), Rename("After"), default);

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id))!;
        Assert.True(applied);
        Assert.Equal("After", stored["Name"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(2, stored["Entries"].AsBsonArray.Count);
    }

    [Fact]
    public async Task A_row_none_of_whose_entries_holds_a_rank_is_as_the_condition_says_whether_the_entries_name_the_rank_or_not()
    {
        var tenant = NewTenant();
        var none = await SeedAsync(tenant);
        var unplaced = await SeedAsync(tenant, new RankingEntry(Guid.NewGuid(), false));
        var withoutTheField = await SeedAsync(tenant, new RankingEntry(Guid.NewGuid(), false));
        await RegistrySeed
            .Collection(_mongo.ConnectionString, RANKINGS)
            .UpdateOneAsync(
                new BsonDocument("_id", RegistrySeed.Binary(withoutTheField)),
                new BsonDocument("$unset", new BsonDocument("Entries.0.Rank", 1))
            );
        var store = Store(tenant);

        var results = new[]
        {
            await store.UpdateWhenAsync(none, RankingFinaliser.NoEntryPlaced(), Rename("Changed"), default),
            await store.UpdateWhenAsync(unplaced, RankingFinaliser.NoEntryPlaced(), Rename("Changed"), default),
            await store.UpdateWhenAsync(withoutTheField, RankingFinaliser.NoEntryPlaced(), Rename("Changed"), default),
        };

        Assert.All(results, Assert.True);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_change_to_a_row_with_a_rank_in_any_of_its_entries_is_not_applied_and_changes_nothing(
        bool firstPlaced,
        bool secondPlaced
    )
    {
        var tenant = NewTenant();
        var id = await SeedAsync(
            tenant,
            new RankingEntry(Guid.NewGuid(), false, firstPlaced ? 1 : null),
            new RankingEntry(Guid.NewGuid(), false, secondPlaced ? 2 : null)
        );
        var before = await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id);

        var applied = await Store(tenant)
            .UpdateWhenAsync(id, RankingFinaliser.NoEntryPlaced(), Rename("After"), default);

        Assert.False(applied);
        Assert.Equal(before, await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id));
    }

    [Fact]
    public async Task Of_many_changes_made_to_a_row_as_the_condition_says_only_one_is_applied()
    {
        var tenant = NewTenant();
        var id = await SeedAsync(
            tenant,
            new RankingEntry(Guid.NewGuid(), false),
            new RankingEntry(Guid.NewGuid(), false)
        );
        var store = Store(tenant);

        var results = await Task.WhenAll(
            Enumerable
                .Range(1, 12)
                .Select(x =>
                    store.UpdateWhenAsync(
                        id,
                        RankingFinaliser.NoEntryPlaced(),
                        new BsonDocument("$set", new BsonDocument("Entries.0.Rank", x)),
                        default
                    )
                )
        );

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id))!;
        Assert.Equal(1, results.Count(x => x));
        Assert.InRange(stored["Entries"][0]["Rank"].AsInt32, 1, 12);
    }

    [Fact]
    public async Task A_change_does_not_reach_the_row_of_another_Tenant_or_a_row_that_is_not_there_whatever_the_condition_says()
    {
        var (tenant, other) = (NewTenant(), NewTenant());
        var id = await SeedAsync(tenant, new RankingEntry(Guid.NewGuid(), false));
        var before = await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id);

        var asOther = await Store(other)
            .UpdateWhenAsync(id, RankingFinaliser.NoEntryPlaced(), Rename("After"), default);
        var missing = await Store(tenant)
            .UpdateWhenAsync(Guid.NewGuid(), RankingFinaliser.NoEntryPlaced(), Rename("After"), default);

        Assert.False(asOther);
        Assert.False(missing);
        Assert.Equal(before, await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id));
    }

    [Fact]
    public async Task No_update_moves_the_row_to_another_Tenant()
    {
        var (tenant, other) = (NewTenant(), NewTenant());
        var id = await SeedAsync(tenant, new RankingEntry(Guid.NewGuid(), false));

        var applied = await Store(tenant)
            .UpdateWhenAsync(
                id,
                RankingFinaliser.NoEntryPlaced(),
                new BsonDocument("$set", new BsonDocument { { "Name", "After" }, { "TenantId", other } }),
                default
            );

        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id))!;
        Assert.True(applied);
        Assert.Equal("After", stored["Name"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
    }

    [Fact]
    public async Task With_no_current_Tenant_the_change_is_refused_and_writes_nothing()
    {
        var tenant = NewTenant();
        var id = await SeedAsync(tenant, new RankingEntry(Guid.NewGuid(), false));
        var before = await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id);

        await Assert.ThrowsAsync<NoCurrentTenantException>(
            () => Store(null).UpdateWhenAsync(id, RankingFinaliser.NoEntryPlaced(), Rename("After"), default)
        );

        Assert.Equal(before, await RegistrySeed.StoredAsync(_mongo.ConnectionString, RANKINGS, id));
    }

    TypedTenantCollection<RankingModel> Store(string? tenant)
    {
        return Tenants().Of<RankingModel>(RANKINGS, tenant);
    }

    TenantCollections Tenants()
    {
        return new TenantCollections(
            new MongoClient(_mongo.ConnectionString),
            Options.Create(new NIdentityOptions { Database = UserSeed.DATABASE })
        );
    }

    async Task<Guid> SeedAsync(string tenant, params RankingEntry[] entries)
    {
        var ranking = new Ranking(
            "Before",
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            Guid.NewGuid(),
            Guid.NewGuid()
        );
        await EventSeed.RankingAsync(_mongo.ConnectionString, tenant, ranking);
        return ranking.Id;
    }

    static BsonDocument Rename(string name)
    {
        return new BsonDocument("$set", new BsonDocument("Name", name));
    }

    static string NewTenant()
    {
        return $"country-{Guid.NewGuid():N}"[..16];
    }
}
