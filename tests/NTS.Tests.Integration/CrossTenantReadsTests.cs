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
/// the search of Athletes, Horses, Clubs and Officials that any signed-in account may make, and the Events a Console
/// Operator runs. Each is a method of its own, takes what it needs and nothing that could widen it, and returns only
/// the fields that are meant to leave: an Athlete's account, if the row has one, never does. The tests seed two
/// Tenants and read as a person who belongs to neither.
/// </summary>
public sealed class CrossTenantReadsTests : IClassFixture<MongoFixture>
{
    static readonly Standing SIGNED_IN = Standing.Account;
    static readonly Standing STAFF = Standing.AtPlatform(Roles.Of(tenantRootIn: ["country-bg"]), false); // a Tenant Root: sets Events up, reads whole rows

    readonly MongoFixture _mongo;

    public CrossTenantReadsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_Event_is_found_by_its_id_in_whatever_Tenant_it_is()
    {
        var tenant = NewTenant();
        var unstarted = await AddSetupEventAsync(tenant);

        var found = await Reads().FindEventAsync(unstarted, default);

        Assert.NotNull(found);
        Assert.Equal(tenant, found.TenantId);
        Assert.False(found.IsStarted);
        Assert.Null(found.EndDay);
    }

    [Fact]
    public async Task A_started_Event_is_the_Core_one_with_the_end_of_its_last_day()
    {
        var tenant = NewTenant();
        var end = new DateTimeOffset(2026, 5, 2, 20, 59, 59, TimeSpan.Zero);
        var id = await AddSetupEventAsync(tenant);
        await AddCoreEventAsync(id, tenant, end);

        var found = await Reads().FindEventAsync(id, default);

        Assert.True(found!.IsStarted);
        Assert.Equal(tenant, found.TenantId);
        Assert.Equal(end, found.EndDay);
        Assert.Equal(EventStage.Live, found.StageAt(end.AddSeconds(-1)));
        Assert.Equal(EventStage.Historic, found.StageAt(end));
    }

    [Fact]
    public async Task An_Event_that_only_the_Core_has_is_found_and_one_that_is_nowhere_is_not()
    {
        var tenant = NewTenant();
        var id = Guid.NewGuid();
        await AddCoreEventAsync(id, tenant, DateTimeOffset.UtcNow.AddDays(1));

        var found = await Reads().FindEventAsync(id, default);
        var missing = await Reads().FindEventAsync(Guid.NewGuid(), default);

        Assert.True(found!.IsStarted);
        Assert.Null(missing);
    }

    [Fact]
    public async Task An_Event_from_before_Tenants_has_the_constant_Tenant()
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
        Assert.Equal("nts", older!.TenantId);
    }

    [Fact]
    public async Task The_Events_a_Console_Operator_runs_that_are_not_Historic_are_told_by_their_Tenants()
    {
        var account = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var inA = NewTenant();
        var inB = NewTenant();
        var onlyHistoric = NewTenant();
        var notConsoleOperator = NewTenant();
        var pending = NewTenant();
        var other = NewTenant();
        var gone = NewTenant();
        await ConsoleOperatorOfAsync(await AddSetupEventAsync(inA), inA, account); // not started
        var live = await AddSetupEventAsync(inB);
        await AddCoreEventAsync(live, inB, now.AddDays(1));
        await ConsoleOperatorOfAsync(live, inB, account);
        var past = await AddSetupEventAsync(onlyHistoric);
        await AddCoreEventAsync(past, onlyHistoric, now.AddDays(-1));
        await ConsoleOperatorOfAsync(past, onlyHistoric, account);
        var operatorOnly = await AddSetupEventAsync(notConsoleOperator); // a grant of another kind gives no Console
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            notConsoleOperator,
            operatorOnly,
            "Operator",
            null,
            "o@x.test",
            account
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            pending,
            await AddSetupEventAsync(pending),
            "ConsoleOperator",
            null,
            "p@x.test",
            null
        );
        await ConsoleOperatorOfAsync(await AddSetupEventAsync(other), other, someoneElse); // somebody else's
        await ConsoleOperatorOfAsync(Guid.NewGuid(), gone, account); // of an Event that is not there

        var tenants = await Reads().OpenEventTenantsOfAsync(account, now, default);

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

        var byName = await Reads().SearchAthletesAsync(STAFF, SearchOf($"ANA"), default);
        var byMarker = await Reads().SearchAthletesAsync(STAFF, SearchOf($"Ana{marker}"), default);
        var byEnglishName = await Reads().SearchAthletesAsync(STAFF, SearchOf($"MARIA{marker}"), default);
        var byFeiId = await Reads().SearchAthletesAsync(STAFF, SearchOf("10000002"), default);

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

        var horses = await Reads().SearchHorsesAsync(SIGNED_IN, SearchOf("stormy"), default);
        var clubs = await Reads().SearchClubsAsync(SIGNED_IN, SearchOf("RIDING"), default);
        var officials = await Reads().SearchOfficialsAsync(SIGNED_IN, SearchOf("georg"), default);

        Assert.Equal(new[] { horse }, horses.Select(x => x.Id));
        Assert.Equal(new[] { club }, clubs.Select(x => x.Id));
        var official = Assert.Single(officials);
        Assert.Equal("GroundJury", official.Role);
        Assert.Equal(b, official.TenantId);
        Assert.Empty(await Reads().SearchHorsesAsync(SIGNED_IN, SearchOf("Sofia"), default));
        Assert.Empty(await Reads().SearchClubsAsync(SIGNED_IN, SearchOf("stormy"), default));
    }

    [Fact]
    public async Task An_Official_who_is_in_many_Events_is_found_once()
    {
        var a = NewTenant();
        await AddAsync("event_officials", a, "Maria Dimitrova", role: "Steward");
        await AddAsync("event_officials", a, "Maria Dimitrova", role: "Steward");
        await AddAsync("event_officials", NewTenant(), "Maria Dimitrova", role: "Steward");
        await AddAsync("event_officials", a, "Maria Dimitrova", role: "ChiefSteward");

        var officials = await Reads().SearchOfficialsAsync(SIGNED_IN, SearchOf("dimitrova"), default);

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

        var found = Assert.Single(await Reads().SearchAthletesAsync(STAFF, SearchOf("secret rider"), default));
        var named = Assert.Single(await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("secret rider"), default));
        var byFeiId = await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("10000009"), default);

        Assert.Equal(id, found.Id);
        Assert.False(found.IsNameOnly);
        Assert.Equal("Rider Club", found.Club);
        Assert.Equal("Bulgaria", found.Country);
        Assert.Equal("10000009", found.FeiId);
        var json = JsonSerializer.Serialize(found);
        Assert.DoesNotContain("secret.rider@example.test", json);
        Assert.DoesNotContain("stamp", json);
        Assert.DoesNotContain("official", json);
        Assert.Equal(id, named.Id); // an account that sets nothing up reads the name, and not what else the row holds
        Assert.True(named.IsNameOnly);
        Assert.Equal("Secret Rider", named.Name);
        Assert.Null(named.NameEnglish);
        Assert.Null(named.FeiId);
        Assert.Null(named.Club);
        Assert.Null(named.Country);
        Assert.Equal(string.Empty, named.TenantId);
        Assert.Empty(byFeiId); // and the FEI ID is not matched either
    }

    [Fact]
    public async Task The_whole_row_of_a_Horse_is_for_the_people_who_set_an_Event_up_and_the_name_for_every_other_account()
    {
        var marker = Guid.NewGuid().ToString("N")[..6];
        var id = await AddAsync("horses", NewTenant(), $"Zephyr{marker} Night", feiId: $"BUL{marker}");

        var whole = Assert.Single(await Reads().SearchHorsesAsync(STAFF, SearchOf($"bul{marker}"), default));
        var named = Assert.Single(await Reads().SearchHorsesAsync(SIGNED_IN, SearchOf($"zephyr{marker}"), default));
        var byFeiId = await Reads().SearchHorsesAsync(SIGNED_IN, SearchOf($"bul{marker}"), default);

        Assert.Equal((id, $"BUL{marker}", false), (whole.Id, whole.FeiId, whole.IsNameOnly));
        Assert.Equal((id, true), (named.Id, named.IsNameOnly));
        Assert.Null(named.FeiId);
        Assert.Empty(byFeiId);
    }

    [Fact]
    public async Task The_text_is_searched_for_as_it_is_written_and_not_as_a_pattern()
    {
        var tenant = NewTenant();
        await AddAsync("athletes", tenant, "Ana Petrova");
        await AddAsync("athletes", tenant, "Dot.Dot Rider");

        var any = await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf(".*."), default);
        var dot = await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("dot.d"), default);
        var brackets = await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("((("), default);

        Assert.Empty(any);
        Assert.Single(dot);
        Assert.Empty(brackets);
    }

    [Fact]
    public async Task A_search_needs_a_word_of_two_characters_and_reads_the_page_it_is_asked_for()
    {
        var tenant = NewTenant();
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 14; i++)
        {
            await AddAsync("athletes", tenant, $"{marker} Rider {i:00}");
        }

        var first = await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf(marker, skip: 0, take: 10), default);
        var second = await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf(marker, skip: 10, take: 10), default);

        Assert.Equal(10, first.Count);
        Assert.Equal(first.Select(x => x.Name).Order(StringComparer.Ordinal), first.Select(x => x.Name));
        Assert.Equal(4, second.Count);
        Assert.Equal(Enumerable.Range(10, 4).Select(i => $"{marker} Rider {i:00}"), second.Select(x => x.Name));
        await Assert.ThrowsAsync<ArgumentException>(
            () => Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("a"), default)
        );
        await Assert.ThrowsAsync<ArgumentException>(
            () => Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("  a  "), default)
        );
        await Assert.ThrowsAsync<ArgumentException>(
            () => Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("a b c"), default)
        );
        await Reads().SearchAthletesAsync(SIGNED_IN, SearchOf("ab"), default); // two characters are enough
    }

    [Fact]
    public async Task Nobody_who_is_not_signed_in_searches_a_registry()
    {
        await AddAsync("athletes", NewTenant(), "Open Rider");

        await Assert.ThrowsAsync<CrossTenantRefusedException>(
            () => Reads().SearchAthletesAsync(Standing.Anonymous, SearchOf("open rider"), default)
        );
        await Assert.ThrowsAsync<CrossTenantRefusedException>(
            () => Reads().SearchClubsAsync(Standing.Anonymous, SearchOf("open rider"), default)
        );
        await Assert.ThrowsAsync<CrossTenantRefusedException>(
            () => Reads().SearchOfficialsAsync(Standing.Anonymous, SearchOf("open rider"), default)
        );
    }

    /// <summary>The search a route would make of a text: its words, in the order of the names, and a page of ten and one row more.</summary>
    static NameSearch SearchOf(string text, int skip = 0, int take = 11)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new NameSearch(words, descending: false, skip, take);
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

    async Task<Guid> AddSetupEventAsync(string tenant)
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Bson(id) },
            { "TenantId", tenant },
            { "Name", "Event " + id },
        };

        await Collection("configure_events").InsertOneAsync(document);
        return id;
    }

    async Task AddCoreEventAsync(Guid id, string tenant, DateTimeOffset end)
    {
        var document = new BsonDocument
        {
            { "_id", Bson(id) },
            { "TenantId", tenant },
            { "Name", "Event " + id },
            { "StartDay", new BsonDateTime(end.AddDays(-2).UtcDateTime) },
            { "EndDay", new BsonDateTime(end.UtcDateTime) },
        };

        await Collection("event_informations").InsertOneAsync(document);
    }

    /// <summary>The account is a Console Operator of the Event, by a grant that is theirs.</summary>
    async Task ConsoleOperatorOfAsync(Guid eventId, string tenant, Guid account)
    {
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "ConsoleOperator",
            null,
            $"{account:N}@x.test",
            account
        );
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
