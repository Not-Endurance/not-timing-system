using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NoTiming.Api.Features.Events;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.Staging;

namespace NTS.Tests.Integration;

/// <summary>
/// #607, <c>seed-staging</c>: a Tenant Root, a Main Operator, Officials, Operators and a Live Event with Participations,
/// in the Tenant of Bulgaria, on a database that is not production. It is a dry run unless it is told to apply, it refuses a
/// database that is marked Production and one that is not marked at all unless it is told which environment it is, it makes
/// everything once, and what it makes is what the Api makes when it starts an Event. Over a MongoDB in a container.
/// </summary>
public sealed class StagingSeedTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2031, 3, 14, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public StagingSeedTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_dry_run_reports_what_it_would_make_and_changes_nothing()
    {
        var data = NewData();
        var before = await data.AllAsync();

        var report = await StagingSeed.Run(data.Database, Seed(dry: true), NOW);

        Assert.Equal(before, await data.AllAsync());
        Assert.False(report.Applied);
        Assert.False(report.Refused);
        Assert.Equal("Staging", report.MarkWith);
        Assert.True(report.CountryMade);
        Assert.True(report.TenantMade);
        Assert.Equal(5, report.AccountsMade.Count);
        Assert.True(report.TenantRootMade);
        Assert.True(report.SetupMade);
        Assert.True(report.CoreMade);
        Assert.Equal(12, report.EventDocuments["event_participations"]);
        Assert.Equal(2, report.EventDocuments["event_rankings"]);
        Assert.Equal(2, report.EventDocuments["event_officials"]);
        Assert.Equal(1, report.EventDocuments["event_operators"]);
        Assert.Equal(3, report.GrantsMade);
        var text = new StringWriter();
        report.WriteTo(text);
        Assert.Contains("Dry-run staging seed.", text.ToString());
        Assert.Contains("Nothing was changed. Run again with --apply to persist.", text.ToString());
        Assert.DoesNotContain("@", text.ToString()); // what an account keeps is not printed
    }

    [Fact]
    public async Task Apply_makes_the_accounts_the_Tenant_and_the_Event_in_the_shapes_the_Api_reads()
    {
        var data = NewData();

        var report = await StagingSeed.Run(data.Database, Seed(), NOW);

        Assert.True(report.Applied);
        Assert.Equal("Staging", await EnvironmentMarker.ReadAsync(data.Database));
        var bulgaria = await data.Collection("countries").Find(new BsonDocument("IsoCode", "BG")).SingleAsync();
        Assert.Equal("Bulgaria", bulgaria["Name"].AsString);
        var tenant = await data.Collection("tenants").Find(new BsonDocument("_id", "country-bg")).SingleAsync();
        Assert.True(tenant["RegionalRules"]["OnlyAverageLoopSpeed"].AsBoolean);

        var accounts = (await data.ManyAsync("users")).ToDictionary(x => x["Email"].AsString);
        Assert.Equal(
            [
                "main@example.test",
                "official.one@example.test",
                "official.two@example.test",
                "operator@example.test",
                "root@example.test",
            ],
            accounts.Keys.Order()
        );
        Assert.All(
            accounts.Values,
            x =>
            {
                Assert.False(x["EmailConfirmed"].AsBoolean); // the code that reaches the address confirms it
                Assert.Equal("country-bg", x["HomeTenantId"].AsString);
                Assert.Equal(
                    "country-bg",
                    Assert.Single(x["Memberships"].AsBsonArray).AsBsonDocument["TenantId"].AsString
                );
            }
        );
        Assert.Equal(["tenant-root"], RolesOf(accounts["root@example.test"]));
        Assert.Empty(RolesOf(accounts["main@example.test"]));

        var eventId = report.EventId;
        var setup = await data.OneAsync("configure_events", eventId);
        var core = await data.OneAsync("event_informations", eventId);
        var main = accounts["main@example.test"]["_id"].AsGuid;
        Assert.Equal(main, setup["MainOperatorId"].AsGuid);
        Assert.Equal(main, core["MainOperatorId"].AsGuid);
        Assert.Equal("country-bg", setup["TenantId"].AsString);
        Assert.Equal("country-bg", core["TenantId"].AsString);
        Assert.Equal(new DateTime(2031, 3, 20, 23, 59, 59, DateTimeKind.Utc), core["EndDay"].ToUniversalTime()); // seven days from today
        foreach (
            var (collection, count) in new[]
            {
                ("event_participations", 12),
                ("event_rankings", 2),
                ("event_officials", 2),
                ("event_operators", 1),
            }
        )
        {
            var documents = await data.ManyAsync(collection);
            Assert.Equal(count, documents.Count);
            Assert.All(
                documents,
                x =>
                {
                    Assert.Equal(eventId, x["EventId"].AsGuid);
                    Assert.Equal("country-bg", x["TenantId"].AsString);
                }
            );
        }
    }

    [Fact]
    public async Task The_grants_are_the_ones_the_Api_reads_and_name_the_accounts()
    {
        var data = NewData();

        var report = await StagingSeed.Run(data.Database, Seed(), NOW);

        var grants = (await data.ManyAsync("event_grants"))
            .Select(GrantDocuments.ToGrant)
            .OfType<EventGrant>()
            .ToList();
        Assert.Equal(3, grants.Count);
        Assert.All(grants, x => Assert.Equal((report.EventId, "country-bg"), (x.EventId, x.TenantId)));
        Assert.All(grants, x => Assert.False(x.IsPending));
        var accounts = (await data.ManyAsync("users")).ToDictionary(x => x["Email"].AsString, x => x["_id"].AsGuid);
        Assert.Contains(
            grants,
            x =>
                x is { Kind: GrantKind.Official, OfficialRole: OfficialRole.Steward }
                && x.AccountId == accounts["official.one@example.test"]
        );
        Assert.Contains(
            grants,
            x =>
                x is { Kind: GrantKind.Official, OfficialRole: OfficialRole.ChiefSteward }
                && x.AccountId == accounts["official.two@example.test"]
        );
        Assert.Contains(
            grants,
            x => x is { Kind: GrantKind.Operator } && x.AccountId == accounts["operator@example.test"]
        );
    }

    [Fact]
    public async Task The_Participations_are_the_ones_the_Api_starts_an_Event_with_and_the_Core_ends_when_asked()
    {
        var data = NewData();

        var report = await StagingSeed.Run(data.Database, Seed(days: 3), NOW);

        var numbers = (await data.ManyAsync("event_participations"))
            .Select(x => x["Combination"]["Number"].AsInt32)
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(1, 12).ToArray(), numbers);
        var core = await data.OneAsync("event_informations", report.EventId);
        Assert.Equal(new DateTime(2031, 3, 16, 23, 59, 59, DateTimeKind.Utc), core["EndDay"].ToUniversalTime());
        Assert.Equal(new DateTimeOffset(2031, 3, 16, 23, 59, 59, TimeSpan.Zero), report.EventEnds);
    }

    [Fact]
    public async Task Running_it_again_makes_nothing_twice()
    {
        var data = NewData();
        await StagingSeed.Run(data.Database, Seed(), NOW);
        var first = await data.AllAsync();

        var again = await StagingSeed.Run(data.Database, Seed(), NOW.AddHours(3));

        Assert.True(again.Applied);
        Assert.Equal(first, await data.AllAsync());
        Assert.False(again.CountryMade);
        Assert.False(again.TenantMade);
        Assert.Empty(again.AccountsMade);
        Assert.Equal(5, again.AccountsKept);
        Assert.False(again.SetupMade);
        Assert.False(again.CoreMade);
        Assert.Empty(again.EventDocuments);
        Assert.Equal(0, again.GrantsMade);
    }

    [Fact]
    public async Task A_run_that_stopped_part_way_is_finished_by_running_it_again()
    {
        var data = NewData();
        var report = await StagingSeed.Run(data.Database, Seed(), NOW);
        await data.Collection("event_participations").DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await data.Collection("event_grants").DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);

        var again = await StagingSeed.Run(data.Database, Seed(), NOW);

        Assert.Equal(12, again.EventDocuments["event_participations"]);
        Assert.Equal(3, again.GrantsMade);
        Assert.Equal(12, await data.Collection("event_participations").CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(3, await data.Collection("event_grants").CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(
            1,
            await data.Collection("event_informations")
                .CountDocumentsAsync(new BsonDocument("_id", LegacyTenantData.Uuid(report.EventId)))
        );
    }

    [Fact]
    public async Task The_same_name_is_the_same_Event_and_another_name_is_another()
    {
        var data = NewData();
        var one = await StagingSeed.Run(data.Database, Seed(), NOW);
        var other = await StagingSeed.Run(data.Database, Seed(name: "Another Event"), NOW);

        Assert.NotEqual(one.EventId, other.EventId);
        Assert.Equal(2, await data.Collection("event_informations").CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(24, await data.Collection("event_participations").CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(5, await data.Collection("users").CountDocumentsAsync(new BsonDocument())); // the same people
        Assert.Equal(6, await data.Collection("event_grants").CountDocumentsAsync(new BsonDocument()));
    }

    [Fact]
    public async Task An_account_that_is_there_keeps_everything_it_has_and_gets_what_the_seed_gives()
    {
        var data = NewData();
        var root = await data.AccountAsync("Root@Example.Test", "Bulgaria", x => x["Club"] = "Sofia Riders");
        var official = await data.AccountAsync(
            "official.one@example.test",
            null,
            x =>
            {
                x["EmailConfirmed"] = true;
                x["SecurityStamp"] = "a-stamp";
            }
        );
        var before = (await data.OneAsync("users", root), await data.OneAsync("users", official));

        var report = await StagingSeed.Run(data.Database, Seed(), NOW);

        Assert.Equal(3, report.AccountsMade.Count);
        Assert.Equal(2, report.AccountsKept);
        var rootAfter = await data.OneAsync("users", root);
        Assert.Equal("Sofia Riders", rootAfter["Club"].AsString);
        Assert.Equal("Root@Example.Test", rootAfter["Email"].AsString); // not rewritten
        Assert.Equal(["tenant-root"], RolesOf(rootAfter));
        Assert.Equal("country-bg", rootAfter["HomeTenantId"].AsString);
        var officialAfter = await data.OneAsync("users", official);
        Assert.True(officialAfter["EmailConfirmed"].AsBoolean);
        Assert.Equal("a-stamp", officialAfter["SecurityStamp"].AsString);
        Assert.Equal("country-bg", officialAfter["HomeTenantId"].AsString);
        Assert.Equal(before.Item2["Unknown"], officialAfter["Unknown"]);
        Assert.Equal(3, await data.Collection("event_grants").CountDocumentsAsync(new BsonDocument()));
    }

    [Fact]
    public async Task An_account_that_has_the_role_already_is_not_given_it_twice()
    {
        var data = NewData();
        var root = await data.AccountAsync(
            "root@example.test",
            "Bulgaria",
            x =>
            {
                x["HomeTenantId"] = "country-bg";
                x["Memberships"] = new BsonArray
                {
                    new BsonDocument
                    {
                        { "TenantId", "country-bg" },
                        {
                            "Roles",
                            new BsonArray { "tenant-root" }
                        },
                    },
                };
            }
        );

        var report = await StagingSeed.Run(data.Database, Seed(), NOW);

        Assert.Equal(["tenant-root"], RolesOf(await data.OneAsync("users", root)));
        Assert.False(report.TenantRootMade);
    }

    [Fact]
    public async Task A_database_that_is_marked_Production_is_refused_and_nothing_is_changed()
    {
        var data = NewData();
        await EnvironmentMarker.WriteAsync(data.Database, "Production", NOW);
        var before = await data.AllAsync();

        var report = await StagingSeed.Run(data.Database, Seed(environment: null), NOW);
        var named = await StagingSeed.Run(data.Database, Seed(environment: "Staging"), NOW);

        Assert.True(report.Refused);
        Assert.Contains(report.Refusals, x => x.Contains("Production"));
        Assert.True(named.Refused);
        Assert.Equal(before, await data.AllAsync());
    }

    [Fact]
    public async Task Nothing_is_seeded_as_Production_and_a_database_with_no_marker_needs_the_environment_it_is()
    {
        var data = NewData();

        var asProduction = await StagingSeed.Run(data.Database, Seed(environment: "Production"), NOW);
        var unnamed = await StagingSeed.Run(data.Database, Seed(environment: null), NOW);
        var unknown = await StagingSeed.Run(data.Database, Seed(environment: "Prod"), NOW);

        Assert.True(asProduction.Refused);
        Assert.True(unnamed.Refused);
        Assert.Contains(unnamed.Refusals, x => x.Contains("--environment"));
        Assert.True(unknown.Refused);
        Assert.Null(await EnvironmentMarker.ReadAsync(data.Database));
        Assert.Empty(await data.AllAsync());
    }

    [Theory]
    [InlineData("Prod")]
    [InlineData("staging-eu")]
    public async Task A_database_whose_marker_is_none_of_the_environments_is_refused_whatever_environment_is_named(
        string marker
    )
    {
        var data = NewData();
        await data.Collection("environment")
            .InsertOneAsync(new BsonDocument { { "_id", "environment" }, { "Name", marker } });
        var before = await data.AllAsync();

        var unnamed = await StagingSeed.Run(data.Database, Seed(environment: null), NOW);
        var named = await StagingSeed.Run(data.Database, Seed(environment: "Staging"), NOW);

        Assert.True(unnamed.Refused);
        Assert.Contains(unnamed.Refusals, x => x.Contains(marker));
        Assert.True(named.Refused);
        Assert.Equal(before, await data.AllAsync());
        Assert.Equal(marker, await EnvironmentMarker.ReadAsync(data.Database));
    }

    [Fact]
    public async Task A_database_marked_as_the_other_non_production_environment_is_refused_and_one_that_says_the_same_is_seeded()
    {
        var data = NewData();
        await EnvironmentMarker.WriteAsync(data.Database, "Development", NOW);

        var other = await StagingSeed.Run(data.Database, Seed(environment: "Staging"), NOW);
        var same = await StagingSeed.Run(data.Database, Seed(environment: "development"), NOW);
        var unnamed = await StagingSeed.Run(data.Database, Seed(environment: null), NOW);

        Assert.True(other.Refused);
        Assert.Contains(other.Refusals, x => x.Contains("Development"));
        Assert.True(same.Applied);
        Assert.True(unnamed.Applied); // the marker says what it is
        Assert.Equal("Development", await EnvironmentMarker.ReadAsync(data.Database));
    }

    [Theory]
    [InlineData("tenant-root", "nobody")]
    [InlineData("main-operator", "")]
    [InlineData("days", "0")]
    [InlineData("days", "61")]
    [InlineData("start", "24:00")]
    [InlineData("name", " ")]
    [InlineData("official", "not an email")]
    public async Task What_is_asked_wrongly_is_refused_and_nothing_is_changed(string what, string value)
    {
        var data = NewData();
        var options = new StagingSeedOptions
        {
            Apply = true,
            Environment = "Staging",
            TenantRoot = what == "tenant-root" ? value : "root@example.test",
            MainOperator = what == "main-operator" ? value : "main@example.test",
            Officials = what == "official" ? [(value, OfficialRole.Steward)] : [],
            Days = what == "days" ? int.Parse(value) : 7,
            StartTime = what == "start" ? TimeSpan.FromHours(24) : TimeSpan.Zero,
            EventName = what == "name" ? value : "Staging Seed Event",
        };

        var report = await StagingSeed.Run(data.Database, options, NOW);

        Assert.True(report.Refused);
        Assert.Empty(await data.AllAsync());
    }

    static string[] RolesOf(BsonDocument account)
    {
        return [.. account["Memberships"].AsBsonArray[0]["Roles"].AsBsonArray.Select(x => x.AsString)];
    }

    static StagingSeedOptions Seed(
        bool dry = false,
        string? environment = "Staging",
        int days = 7,
        string name = "Staging Seed Event"
    )
    {
        return new StagingSeedOptions
        {
            Apply = !dry,
            Environment = environment,
            TenantRoot = "root@example.test",
            MainOperator = "Main@Example.Test",
            Officials =
            [
                ("official.one@example.test", OfficialRole.Steward),
                ("Official.Two@Example.Test", OfficialRole.ChiefSteward),
            ],
            Operators = ["operator@example.test"],
            EventName = name,
            Days = days,
        };
    }

    LegacyTenantData NewData()
    {
        return new LegacyTenantData(
            new MongoClient(_mongo.ConnectionString).GetDatabase("seed-" + Guid.NewGuid().ToString("N"))
        );
    }
}
