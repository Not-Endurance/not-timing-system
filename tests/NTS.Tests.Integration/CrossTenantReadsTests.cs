using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Access;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// What reaches across Tenants does it as a named capability of a collection (ADR-0012, #643): an Event by its id,
/// the search of Athletes, Horses, Clubs and Officials that any signed-in account may make, and the Events a Main
/// Operator runs. Each is a method of its own, takes what it needs and nothing that could widen it, and returns only
/// the fields that are meant to leave: an Athlete's account, if the row has one, never does. The tests seed two
/// Tenants and read as a person who belongs to neither.
/// </summary>
public sealed class CrossTenantReadsTests : IClassFixture<MongoFixture>
{
    static readonly Caller SIGNED_IN = Caller.Of(TestId.Of(1));

    readonly MongoFixture _mongo;

    public CrossTenantReadsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_Event_is_found_by_its_id_in_whatever_Tenant_it_is()
    {
        var tenant = NewTenant();
        var mainOperator = Guid.NewGuid();
        var unstarted = await AddSetupEventAsync(tenant, mainOperator);

        var found = await Reads().FindEventAsync(unstarted, default);

        Assert.NotNull(found);
        Assert.Equal(tenant, found.TenantId);
        Assert.Equal(mainOperator, found.MainOperatorId);
        Assert.False(found.IsStarted);
        Assert.Null(found.EndDay);
    }

    [Fact]
    public async Task A_started_Event_is_the_Core_one_and_its_Main_Operator_and_Tenant_are_the_ones_it_has_now()
    {
        var tenant = NewTenant();
        var first = Guid.NewGuid();
        var handedTo = Guid.NewGuid();
        var end = new DateTimeOffset(2026, 5, 2, 20, 59, 59, TimeSpan.Zero);
        var id = await AddSetupEventAsync(tenant, first);
        await AddCoreEventAsync(id, tenant, handedTo, end);

        var found = await Reads().FindEventAsync(id, default);

        Assert.True(found!.IsStarted);
        Assert.Equal(handedTo, found.MainOperatorId);
        Assert.Equal(end, found.EndDay);
        Assert.Equal(EventStage.Live, found.StageAt(end.AddSeconds(-1)));
        Assert.Equal(EventStage.Historic, found.StageAt(end));
    }

    [Fact]
    public async Task An_Event_that_only_the_Core_has_is_found_and_one_that_is_nowhere_is_not()
    {
        var tenant = NewTenant();
        var id = Guid.NewGuid();
        await AddCoreEventAsync(id, tenant, null, DateTimeOffset.UtcNow.AddDays(1));

        var found = await Reads().FindEventAsync(id, default);
        var missing = await Reads().FindEventAsync(Guid.NewGuid(), default);

        Assert.True(found!.IsStarted);
        Assert.Null(found.MainOperatorId);
        Assert.Null(missing);
    }

    [Fact]
    public async Task An_Event_from_before_Tenants_has_the_constant_Tenant_and_no_Main_Operator()
    {
        var id = Guid.NewGuid();
        await Collection("configure_events")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", Bson(id) },
                    { "TenantId", "nts" },
                    { "Name", "Old" },
                }
            );
        var unmarked = Guid.NewGuid();
        await Collection("configure_events")
            .InsertOneAsync(new BsonDocument { { "_id", Bson(unmarked) }, { "Name", "Older" } });

        var legacy = await Reads().FindEventAsync(id, default);
        var older = await Reads().FindEventAsync(unmarked, default);

        Assert.Equal("nts", legacy!.TenantId);
        Assert.Null(legacy.MainOperatorId);
        Assert.Equal("nts", older!.TenantId);
    }

    [Fact]
    public async Task The_Events_a_Main_Operator_runs_that_are_not_Historic_are_told_by_their_Tenants()
    {
        var mainOperator = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var inA = NewTenant();
        var inB = NewTenant();
        var onlyHistoric = NewTenant();
        await AddSetupEventAsync(inA, mainOperator);
        var live = await AddSetupEventAsync(inB, Guid.NewGuid()); // set up by someone else, handed over once started
        await AddCoreEventAsync(live, inB, mainOperator, now.AddDays(1));
        var past = await AddSetupEventAsync(onlyHistoric, Guid.NewGuid());
        await AddCoreEventAsync(past, onlyHistoric, mainOperator, now.AddDays(-1));
        var handedAway = await AddSetupEventAsync(NewTenant(), mainOperator); // the Core event says somebody else holds it now
        await AddCoreEventAsync(handedAway, "x", Guid.NewGuid(), now.AddDays(1));
        await AddSetupEventAsync(NewTenant(), Guid.NewGuid()); // somebody else's, not started
        await AddSetupEventAsync(NewTenant(), null); // nobody's, not started

        var tenants = await Reads().OpenEventTenantsOfAsync(mainOperator, now, default);

        Assert.Equal(new[] { inA, inB }.Order(), tenants.Order());
    }

    [Fact]
    public async Task Athletes_are_found_across_Tenants_by_name_in_any_case_by_the_English_name_or_by_the_FEI_ID()
    {
        var a = NewTenant();
        var b = NewTenant();
        var marker = Guid.NewGuid().ToString("N")[..6];
        var ana = await AddAsync("athletes", a, $"Ana{marker} Petrova", feiId: "10000001");
        var anastasia = await AddAsync("athletes", b, $"Anastasia{marker} Ivanova", feiId: "10000002");
        var maria = await AddAsync(
            "athletes",
            a,
            "Мария Попова",
            nameEnglish: $"Maria{marker} Popova",
            feiId: "10000004"
        );
        await AddAsync("athletes", a, $"Boris{marker} Georgiev", feiId: "10000003");

        var byName = await Reads().SearchAthletesAsync(SIGNED_IN, $"ANA", default);
        var byMarker = await Reads().SearchAthletesAsync(SIGNED_IN, $"Ana{marker}", default);
        var byEnglishName = await Reads().SearchAthletesAsync(SIGNED_IN, $"MARIA{marker}", default);
        var byFeiId = await Reads().SearchAthletesAsync(SIGNED_IN, "10000002", default);

        Assert.Contains(ana, byName.Select(x => x.Id));
        Assert.Contains(anastasia, byName.Select(x => x.Id));
        Assert.Equal(new[] { ana }, byMarker.Select(x => x.Id));
        Assert.Equal(new[] { maria }, byEnglishName.Select(x => x.Id));
        Assert.Contains(anastasia, byFeiId.Select(x => x.Id));
        Assert.Contains(byName, x => x.TenantId == a);
        Assert.Contains(byName, x => x.TenantId == b);
    }

    [Fact]
    public async Task Horses_and_Clubs_and_Officials_are_found_the_same_way_each_through_its_own_method()
    {
        var a = NewTenant();
        var b = NewTenant();
        var horse = await AddAsync("horses", b, "Stormy Night", feiId: "BUL12345");
        var club = await AddAsync("clubs", a, "Sofia Riding Club");
        await AddAsync("event_officials", b, "Georgi Georgiev", role: "GroundJury");

        var horses = await Reads().SearchHorsesAsync(SIGNED_IN, "stormy", default);
        var clubs = await Reads().SearchClubsAsync(SIGNED_IN, "RIDING", default);
        var officials = await Reads().SearchOfficialsAsync(SIGNED_IN, "georg", default);

        Assert.Equal(new[] { horse }, horses.Select(x => x.Id));
        Assert.Equal(new[] { club }, clubs.Select(x => x.Id));
        var official = Assert.Single(officials);
        Assert.Equal("GroundJury", official.Role);
        Assert.Equal(b, official.TenantId);
        Assert.Empty(await Reads().SearchHorsesAsync(SIGNED_IN, "Sofia", default));
        Assert.Empty(await Reads().SearchClubsAsync(SIGNED_IN, "stormy", default));
    }

    [Fact]
    public async Task An_Official_who_is_in_many_Events_is_found_once()
    {
        var a = NewTenant();
        await AddAsync("event_officials", a, "Maria Dimitrova", role: "Steward");
        await AddAsync("event_officials", a, "Maria Dimitrova", role: "Steward");
        await AddAsync("event_officials", NewTenant(), "Maria Dimitrova", role: "Steward");
        await AddAsync("event_officials", a, "Maria Dimitrova", role: "ChiefSteward");

        var officials = await Reads().SearchOfficialsAsync(SIGNED_IN, "dimitrova", default);

        Assert.Equal(["ChiefSteward", "Steward"], officials.Select(x => x.Role!).Order());
    }

    [Fact]
    public async Task What_is_returned_is_the_fields_that_are_meant_to_leave_and_never_the_account_a_row_has()
    {
        var tenant = NewTenant();
        var id = await AddAsync(
            "athletes",
            tenant,
            "Secret Rider",
            feiId: "10000009",
            extra: new BsonDocument
            {
                {
                    "Club",
                    new BsonDocument { { "Name", "Rider Club" }, { "TenantId", tenant } }
                },
                {
                    "Country",
                    new BsonDocument { { "Name", "Bulgaria" }, { "IsoCode", "BG" } }
                },
                {
                    "User",
                    new BsonDocument
                    {
                        { "Email", "secret.rider@example.test" },
                        {
                            "Roles",
                            new BsonArray { "official" }
                        },
                        { "SecurityStamp", "stamp" },
                    }
                },
            }
        );

        var found = Assert.Single(await Reads().SearchAthletesAsync(SIGNED_IN, "secret rider", default));

        Assert.Equal(id, found.Id);
        Assert.Equal("Rider Club", found.Club);
        Assert.Equal("Bulgaria", found.Country);
        Assert.Equal("10000009", found.FeiId);
        var json = JsonSerializer.Serialize(found);
        Assert.DoesNotContain("secret.rider@example.test", json);
        Assert.DoesNotContain("stamp", json);
        Assert.DoesNotContain("official", json);
    }

    [Fact]
    public async Task The_text_is_searched_for_as_it_is_written_and_not_as_a_pattern()
    {
        var tenant = NewTenant();
        await AddAsync("athletes", tenant, "Ana Petrova");
        await AddAsync("athletes", tenant, "Dot.Dot Rider");

        var any = await Reads().SearchAthletesAsync(SIGNED_IN, ".*.", default);
        var dot = await Reads().SearchAthletesAsync(SIGNED_IN, "dot.d", default);
        var brackets = await Reads().SearchAthletesAsync(SIGNED_IN, "(((", default);

        Assert.Empty(any);
        Assert.Single(dot);
        Assert.Empty(brackets);
    }

    [Fact]
    public async Task A_search_needs_at_least_three_characters_and_returns_at_most_ten_matches()
    {
        var tenant = NewTenant();
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 14; i++)
        {
            await AddAsync("athletes", tenant, $"{marker} Rider {i:00}");
        }

        var found = await Reads().SearchAthletesAsync(SIGNED_IN, marker, default);

        Assert.Equal(10, found.Count);
        Assert.Equal(found.Select(x => x.Name).Order(StringComparer.Ordinal), found.Select(x => x.Name));
        await Assert.ThrowsAsync<ArgumentException>(() => Reads().SearchAthletesAsync(SIGNED_IN, "ab", default));
        await Assert.ThrowsAsync<ArgumentException>(() => Reads().SearchAthletesAsync(SIGNED_IN, "  a  ", default));
    }

    [Fact]
    public async Task Nobody_who_is_not_signed_in_searches_a_registry()
    {
        await AddAsync("athletes", NewTenant(), "Open Rider");

        await Assert.ThrowsAsync<CrossTenantRefusedException>(
            () => Reads().SearchAthletesAsync(Caller.Anonymous, "open rider", default)
        );
        await Assert.ThrowsAsync<CrossTenantRefusedException>(
            () => Reads().SearchClubsAsync(Caller.Anonymous, "open rider", default)
        );
    }

    CrossTenantReads Reads()
    {
        return new CrossTenantReads(
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

    static BsonBinaryData Bson(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    async Task<Guid> AddSetupEventAsync(string tenant, Guid? mainOperator)
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Bson(id) },
            { "TenantId", tenant },
            { "Name", "Event " + id },
        };
        if (mainOperator != null)
        {
            document["MainOperatorId"] = Bson(mainOperator.Value);
        }

        await Collection("configure_events").InsertOneAsync(document);
        return id;
    }

    async Task AddCoreEventAsync(Guid id, string tenant, Guid? mainOperator, DateTimeOffset end)
    {
        var document = new BsonDocument
        {
            { "_id", Bson(id) },
            { "TenantId", tenant },
            { "Name", "Event " + id },
            { "StartDay", new BsonDateTime(end.AddDays(-2).UtcDateTime) },
            { "EndDay", new BsonDateTime(end.UtcDateTime) },
        };
        if (mainOperator != null)
        {
            document["MainOperatorId"] = Bson(mainOperator.Value);
        }

        await Collection("event_informations").InsertOneAsync(document);
    }

    async Task<Guid> AddAsync(
        string collection,
        string tenant,
        string name,
        string? nameEnglish = null,
        string? feiId = null,
        string? role = null,
        BsonDocument? extra = null
    )
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Bson(id) },
            { "TenantId", tenant },
            { "Name", name },
        };
        if (nameEnglish != null)
        {
            document["NameEnglish"] = nameEnglish;
        }

        if (feiId != null)
        {
            document["FeiId"] = feiId;
        }

        if (role != null)
        {
            document["Role"] = role;
        }

        if (extra != null)
        {
            document.AddRange(extra);
        }

        await Collection(collection).InsertOneAsync(document);
        return id;
    }
}
