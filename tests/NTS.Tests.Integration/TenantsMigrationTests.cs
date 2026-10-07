using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.Tenants;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0012, #607, <c>migrate-tenants</c>: the data of before Tenants becomes the data of Tenants. Accounts get the fields
/// of Identity with their address unconfirmed and a home Tenant from the country of their profile, the Tenants are made,
/// every document a Tenant owns is stamped with the Tenant of Bulgaria, and the database is marked with the environment it
/// is. The command is a dry run unless it is told to apply, applies only when it is told which environment the database is,
/// and running it again changes nothing. It runs here over documents in the shape the code before stored them, on a MongoDB
/// in a container.
/// </summary>
public sealed class TenantsMigrationTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2031, 3, 14, 12, 0, 0, TimeSpan.Zero);
    static readonly string[] OWNED =
    [
        "athletes",
        "horses",
        "clubs",
        "configure_events",
        "event_informations",
        "event_officials",
        "event_operators",
        "event_participations",
        "event_rankings",
        "event_handouts",
        "event_grants",
    ];

    readonly MongoFixture _mongo;

    public TenantsMigrationTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public void The_collections_it_stamps_are_the_ones_the_Api_opens_by_Tenant()
    {
        Assert.Equal(
            NoTiming.Api.Features.Tenancy.TenantOwned.Collections.Order(StringComparer.Ordinal),
            TenantsMigration.Owned.Order(StringComparer.Ordinal)
        );
        Assert.Equal(OWNED.Order(StringComparer.Ordinal), TenantsMigration.Owned.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_dry_run_reports_what_it_would_do_and_changes_nothing()
    {
        var data = await LegacyAsync();
        await data.CountryAsync("Türkiye", "TR", "TUR");
        await data.AccountAsync("Ivan@Example.Test", "Bulgaria");
        await data.AccountAsync("maria@example.test", "TR");
        await data.AccountAsync("nobody@example.test");
        await data.OwnedAsync("athletes");
        await data.OwnedAsync("event_participations");
        await data.SettingsAsync(2);
        var before = await data.AllAsync();

        var report = await TenantsMigration.Run(data.Database, new TenantsOptions(), NOW);

        Assert.Equal(before, await data.AllAsync());
        Assert.False(report.Applied);
        Assert.False(report.Refused);
        Assert.Equal(3, report.Accounts);
        Assert.Equal(3, report.AccountsCompleted);
        Assert.Equal([("country-bg", 1), ("country-tr", 1)], PlacedOf(report));
        Assert.Equal(1, report.Unplaced);
        Assert.Equal(["country-bg", "country-tr"], report.TenantsMade.Order());
        Assert.Equal(1, report.Stamped["athletes"]);
        Assert.Equal(1, report.Stamped["event_participations"]);
        Assert.Equal(2, report.SettingsDocuments);
        Assert.False(report.SettingsDropped);
        Assert.Null(report.MarkedAs);
    }

    [Fact]
    public async Task A_dry_run_prints_the_report_and_says_that_nothing_was_changed()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("ivan@example.test", "Bulgaria");

        var report = await TenantsMigration.Run(data.Database, new TenantsOptions(), NOW);

        var output = new StringWriter();
        report.WriteTo(output);
        var text = output.ToString();
        Assert.Contains("Dry-run tenants migration", text);
        Assert.Contains("users: 1 documents read", text);
        Assert.Contains("country-bg", text);
        Assert.Contains("Nothing was changed. Run again with --apply to persist.", text);
        Assert.DoesNotContain("ivan@example.test", text); // what an account keeps is not printed
    }

    [Fact]
    public async Task Apply_is_refused_without_the_environment_the_database_is_and_writes_nothing()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("ivan@example.test", "Bulgaria");
        var before = await data.AllAsync();

        var report = await TenantsMigration.Run(data.Database, new TenantsOptions { Apply = true }, NOW);

        Assert.True(report.Refused);
        Assert.False(report.Applied);
        Assert.Contains(report.Refusals, x => x.Contains("--environment"));
        Assert.Equal(before, await data.AllAsync());
    }

    [Theory]
    [InlineData("Prod")]
    [InlineData("local")]
    public async Task Apply_is_refused_for_a_name_that_is_not_an_environment(string name)
    {
        var data = await LegacyAsync();

        var report = await TenantsMigration.Run(
            data.Database,
            new TenantsOptions { Apply = true, Environment = name },
            NOW
        );

        Assert.True(report.Refused);
        Assert.Null(await EnvironmentMarker.ReadAsync(data.Database));
    }

    [Fact]
    public async Task A_database_marked_as_another_environment_is_refused_and_keeps_its_marker()
    {
        var data = await LegacyAsync();
        await EnvironmentMarker.WriteAsync(data.Database, "Production", NOW);
        var before = await data.AllAsync();

        var report = await TenantsMigration.Run(
            data.Database,
            new TenantsOptions { Apply = true, Environment = "Staging" },
            NOW
        );

        Assert.True(report.Refused);
        Assert.Contains(report.Refusals, x => x.Contains("Production"));
        Assert.Equal(before, await data.AllAsync());
    }

    [Fact]
    public async Task Apply_marks_the_database_with_the_environment_before_it_does_anything_else_and_a_dry_run_only_says_so()
    {
        var data = await LegacyAsync();

        var dry = await TenantsMigration.Run(data.Database, new TenantsOptions { Environment = "staging" }, NOW);
        Assert.Equal("Staging", dry.MarkWith);
        Assert.Null(await EnvironmentMarker.ReadAsync(data.Database));

        var applied = await TenantsMigration.Run(
            data.Database,
            new TenantsOptions { Apply = true, Environment = "staging" },
            NOW
        );

        Assert.True(applied.Applied);
        Assert.Equal("Staging", await EnvironmentMarker.ReadAsync(data.Database));
    }

    [Fact]
    public async Task Running_it_again_changes_nothing()
    {
        var data = await LegacyAsync();
        await data.CountryAsync("Türkiye", "TR", "TUR");
        await data.AccountAsync("Ivan@Example.Test", "Bulgaria");
        await data.AccountAsync("maria@example.test", "Türkiye");
        await data.OwnedAsync("athletes");
        await data.OwnedAsync("event_participations");
        await data.SettingsAsync(1);
        var options = new TenantsOptions { Apply = true, Environment = "Staging" };
        await TenantsMigration.Run(data.Database, options, NOW);
        var afterTheFirst = await data.AllAsync();

        var again = await TenantsMigration.Run(data.Database, options, NOW.AddHours(1));

        Assert.Equal(afterTheFirst, await data.AllAsync());
        Assert.True(again.Applied);
        Assert.Equal(0, again.AccountsCompleted);
        Assert.Empty(again.Placed);
        Assert.Empty(again.TenantsMade);
        Assert.Empty(again.TenantsCompleted);
        Assert.All(again.Stamped.Values, x => Assert.Equal(0, x));
        Assert.Equal(0, again.SettingsDocuments);
        Assert.Empty(again.IndexesMade);
    }

    [Fact]
    public async Task The_accounts_with_the_same_email_are_listed_by_the_id_of_their_document_and_stop_an_apply()
    {
        var data = await LegacyAsync();
        var one = await data.AccountAsync("ivan@example.test", "Bulgaria");
        var other = await data.AccountAsync(" Ivan@Example.TEST ", "Bulgaria");
        await data.AccountAsync("maria@example.test", "Bulgaria");
        var before = await data.AllAsync();

        var dry = await TenantsMigration.Run(data.Database, new TenantsOptions(), NOW);
        var applied = await TenantsMigration.Run(
            data.Database,
            new TenantsOptions { Apply = true, Environment = "Staging" },
            NOW
        );

        var group = Assert.Single(dry.DuplicateEmails);
        Assert.Equal(new[] { one, other }.Order(), group.Order());
        Assert.False(dry.Refused); // a dry run only lists
        Assert.True(applied.Refused);
        Assert.Contains(applied.Refusals, x => x.Contains("same email"));
        Assert.Equal(before, await data.AllAsync());
        var text = new StringWriter();
        dry.WriteTo(text);
        Assert.Contains(one.ToString(), text.ToString());
        Assert.DoesNotContain("example.test", text.ToString());
    }

    [Fact]
    public async Task An_account_gets_the_fields_of_identity_with_its_email_unconfirmed_and_all_else_of_it_is_kept()
    {
        var data = await LegacyAsync();
        var id = await data.AccountAsync(" Ivan.Petrov@Example.TEST ", "Bulgaria", x => x["Club"] = "Sofia Riders");
        var before = await data.OneAsync("users", id);

        await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        var after = await data.OneAsync("users", id);
        Assert.Equal("ivan.petrov@example.test", after["Email"].AsString);
        Assert.False(after["EmailConfirmed"].AsBoolean);
        Assert.False(string.IsNullOrWhiteSpace(after["SecurityStamp"].AsString));
        Assert.False(after["LockoutEnabled"].AsBoolean);
        Assert.Equal(0, after["AccessFailedCount"].AsInt32);
        foreach (var name in new[] { "Name", "Unknown", "Club", "CountryRegion", "Roles" })
        {
            Assert.Equal(before[name], after[name]);
        }
    }

    [Fact]
    public async Task An_account_that_has_fields_of_identity_keeps_them()
    {
        var data = await LegacyAsync();
        var id = await data.AccountAsync(
            "ivan@example.test",
            "Bulgaria",
            x =>
            {
                x["EmailConfirmed"] = true;
                x["SecurityStamp"] = "a-stamp";
                x["LockoutEnabled"] = true;
                x["AccessFailedCount"] = 3;
            }
        );

        await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        var after = await data.OneAsync("users", id);
        Assert.True(after["EmailConfirmed"].AsBoolean);
        Assert.Equal("a-stamp", after["SecurityStamp"].AsString);
        Assert.True(after["LockoutEnabled"].AsBoolean);
        Assert.Equal(3, after["AccessFailedCount"].AsInt32);
    }

    [Fact]
    public async Task An_account_with_no_email_is_listed_and_left_as_it_is()
    {
        var data = await LegacyAsync();
        var none = await data.AccountAsync(null, "Bulgaria");
        var blank = await data.AccountAsync("   ", "Bulgaria");
        var before = (await data.OneAsync("users", none), await data.OneAsync("users", blank));

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        Assert.Equal(new[] { none, blank }.Order(), report.AccountsWithoutEmail.Order());
        Assert.Equal(before.Item1, await data.OneAsync("users", none));
        Assert.Equal(before.Item2, await data.OneAsync("users", blank));
        Assert.False(report.Refused);
    }

    [Fact]
    public async Task An_account_whose_id_is_not_a_standard_UUID_is_counted_and_left_as_it_is()
    {
        var data = await LegacyAsync();
        await data.Collection("users")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", 7 },
                    { "Email", "Old@Example.Test" },
                    { "CountryRegion", "Bulgaria" },
                }
            );
        await data.Collection("users")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.CSharpLegacy) },
                    { "Email", "legacy@example.test" },
                }
            );
        // A placeholder, not an account. The driver gives a new id to a document whose id is empty, so it is inserted by the command.
        await data.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument
            {
                { "insert", "users" },
                {
                    "documents",
                    new BsonArray
                    {
                        new BsonDocument
                        {
                            { "_id", LegacyTenantData.Uuid(Guid.Empty) },
                            { "Email", "empty@example.test" },
                        },
                    }
                },
            }
        );
        var before = await data.ManyAsync("users");

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        Assert.Equal(3, report.AccountsWithOtherIds);
        Assert.Equal(before, await data.ManyAsync("users"));
        Assert.False(report.Refused);
    }

    [Fact]
    public async Task The_home_Tenant_is_derived_from_the_country_of_the_profile_the_way_the_profile_screen_finds_it()
    {
        var data = await LegacyAsync();
        await data.CountryAsync("Türkiye", "TR", "TUR");
        var byName = await data.AccountAsync("a@example.test", "Bulgaria");
        var byIso = await data.AccountAsync("b@example.test", "bg");
        var byNf = await data.AccountAsync("c@example.test", "BUL");
        var turkish = await data.AccountAsync("d@example.test", "Türkiye");
        var unknown = await data.AccountAsync("e@example.test", "Atlantis");
        var none = await data.AccountAsync("f@example.test");

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        foreach (var id in new[] { byName, byIso, byNf })
        {
            var account = await data.OneAsync("users", id);
            Assert.Equal("country-bg", account["HomeTenantId"].AsString);
            var membership = Assert.Single(account["Memberships"].AsBsonArray).AsBsonDocument;
            Assert.Equal("country-bg", membership["TenantId"].AsString);
            Assert.Empty(membership["Roles"].AsBsonArray);
        }

        Assert.Equal("country-tr", (await data.OneAsync("users", turkish))["HomeTenantId"].AsString);
        Assert.False((await data.OneAsync("users", unknown)).Contains("HomeTenantId"));
        Assert.False((await data.OneAsync("users", none)).Contains("Memberships"));
        Assert.Equal([("country-bg", 3), ("country-tr", 1)], PlacedOf(report));
        Assert.Equal(2, report.Unplaced);
    }

    [Fact]
    public async Task An_account_that_has_a_home_Tenant_is_not_moved_and_gets_the_membership_it_lacks()
    {
        var data = await LegacyAsync();
        await data.CountryAsync("Türkiye", "TR", "TUR");
        var placed = await data.AccountAsync("a@example.test", "Bulgaria", x => x["HomeTenantId"] = "country-tr");
        var member = await data.AccountAsync(
            "b@example.test",
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

        await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        var moved = await data.OneAsync("users", placed);
        Assert.Equal("country-tr", moved["HomeTenantId"].AsString); // the country of the profile does not move it
        Assert.Equal("country-tr", Assert.Single(moved["Memberships"].AsBsonArray).AsBsonDocument["TenantId"].AsString);
        var kept = await data.OneAsync("users", member);
        var membership = Assert.Single(kept["Memberships"].AsBsonArray).AsBsonDocument;
        Assert.Equal(["tenant-root"], membership["Roles"].AsBsonArray.Select(x => x.AsString));
    }

    [Fact]
    public async Task The_Tenant_of_Bulgaria_is_made_with_its_rules_and_the_Tenant_of_another_country_with_none()
    {
        var data = await LegacyAsync();
        await data.CountryAsync("Türkiye", "TR", "TUR");
        await data.AccountAsync("a@example.test", "Türkiye");

        await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        var tenants = (await data.ManyAsync("tenants")).ToDictionary(x => x["_id"].AsString);
        Assert.Equal(["country-bg", "country-tr"], tenants.Keys.Order());
        Assert.Equal("Bulgaria", tenants["country-bg"]["Name"].AsString);
        Assert.Equal("country", tenants["country-bg"]["Kind"].AsString);
        Assert.True(tenants["country-bg"]["RegionalRules"]["OnlyAverageLoopSpeed"].AsBoolean);
        Assert.Equal("Türkiye", tenants["country-tr"]["Name"].AsString);
        Assert.False(tenants["country-tr"].Contains("RegionalRules"));
    }

    [Fact]
    public async Task A_Tenant_that_exists_is_completed_and_never_overwritten()
    {
        var data = await LegacyAsync();
        await data.CountryAsync("Türkiye", "TR", "TUR");
        await data.AccountAsync("a@example.test", "Türkiye");
        await data.Collection("tenants")
            .InsertManyAsync(
                [
                    new BsonDocument
                    {
                        { "_id", "country-bg" },
                        { "Name", "Federation of Bulgaria" },
                        { "Kind", "country" },
                    },
                    new BsonDocument { { "_id", "country-tr" }, { "Name", "Turkish Federation" } },
                ]
            );

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        var bulgaria = await data.Collection("tenants").Find(new BsonDocument("_id", "country-bg")).SingleAsync();
        Assert.Equal("Federation of Bulgaria", bulgaria["Name"].AsString); // a name that is there stays
        Assert.True(bulgaria["RegionalRules"]["OnlyAverageLoopSpeed"].AsBoolean); // the rules it lacked are given
        var turkey = await data.Collection("tenants").Find(new BsonDocument("_id", "country-tr")).SingleAsync();
        Assert.Equal("Turkish Federation", turkey["Name"].AsString);
        Assert.Equal("country", turkey["Kind"].AsString);
        Assert.Equal(["country-bg", "country-tr"], report.TenantsCompleted.Order());
        Assert.Empty(report.TenantsMade);
    }

    [Fact]
    public async Task The_rules_a_Tenant_Root_set_on_the_Tenant_of_Bulgaria_are_left_as_they_are()
    {
        var data = await LegacyAsync();
        await data.Collection("tenants")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", "country-bg" },
                    { "Name", "Bulgaria" },
                    { "Kind", "country" },
                    {
                        "RegionalRules",
                        new BsonDocument { { "OnlyAverageLoopSpeed", false }, { "RankerCode", "XX" } }
                    },
                }
            );

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        var bulgaria = await data.Collection("tenants").Find(new BsonDocument("_id", "country-bg")).SingleAsync();
        Assert.False(bulgaria["RegionalRules"]["OnlyAverageLoopSpeed"].AsBoolean);
        Assert.Equal("XX", bulgaria["RegionalRules"]["RankerCode"].AsString);
        Assert.Empty(report.TenantsCompleted);
    }

    [Fact]
    public async Task The_countries_without_an_ISO_code_are_listed_and_the_ones_that_have_one_make_a_Tenant()
    {
        var data = await LegacyAsync();
        var atlantis = await data.CountryAsync("Atlantis", null);

        var report = await TenantsMigration.Run(data.Database, new TenantsOptions(), NOW);

        Assert.Equal([atlantis], report.CountriesWithoutIsoCode);
        Assert.Equal(2, report.Countries);
        Assert.True(report.HasBulgaria);
        Assert.False(report.Refused);
    }

    [Fact]
    public async Task With_no_countries_the_dry_run_says_so_and_an_apply_is_refused_because_Bulgaria_is_not_there()
    {
        var data = new LegacyTenantData(NewDatabase());
        await data.AccountAsync("ivan@example.test", "Bulgaria");
        var before = await data.AllAsync();

        var dry = await TenantsMigration.Run(data.Database, new TenantsOptions(), NOW);
        var applied = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        Assert.Equal(0, dry.Countries);
        Assert.False(dry.HasBulgaria);
        Assert.False(dry.Refused);
        Assert.True(applied.Refused);
        Assert.Contains(applied.Refusals, x => x.Contains("Bulgaria"));
        Assert.Equal(before, await data.AllAsync());
        var text = new StringWriter();
        dry.WriteTo(text);
        Assert.Contains("no countries", text.ToString());
    }

    [Fact]
    public async Task Every_document_a_Tenant_owns_that_has_the_constant_Tenant_or_none_gets_the_Tenant_of_Bulgaria()
    {
        var data = await LegacyAsync();
        var stamped = new Dictionary<string, (Guid Constant, Guid None, Guid Empty, Guid Other)>();
        foreach (var collection in OWNED)
        {
            // a grant is one person in one place, so each of these is somebody else
            Action<BsonDocument> person = x =>
            {
                if (collection == "event_grants")
                {
                    x["Kind"] = "Operator";
                    x["Email"] = Guid.NewGuid() + "@example.test";
                }
            };
            stamped[collection] = (
                await data.OwnedAsync(collection, "nts", person),
                await data.OwnedAsync(collection, null, person),
                await data.OwnedAsync(collection, "", person),
                await data.OwnedAsync(collection, "country-tr", person)
            );
        }

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        foreach (var (collection, ids) in stamped)
        {
            Assert.Equal("country-bg", (await data.OneAsync(collection, ids.Constant))["TenantId"].AsString);
            Assert.Equal("country-bg", (await data.OneAsync(collection, ids.None))["TenantId"].AsString);
            Assert.Equal("country-bg", (await data.OneAsync(collection, ids.Empty))["TenantId"].AsString);
            Assert.Equal("country-tr", (await data.OneAsync(collection, ids.Other))["TenantId"].AsString); // another Tenant's
            Assert.Equal(3, report.Stamped[collection]);
        }
    }

    [Fact]
    public async Task What_no_Tenant_owns_is_not_stamped()
    {
        var data = await LegacyAsync();
        var country = (await data.ManyAsync("countries")).Single();
        var session = await data.SessionAsync("someone", Guid.NewGuid());

        await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        Assert.Equal("nts", (await data.OneAsync("countries", country["_id"].AsGuid))["TenantId"].AsString);
        Assert.Equal(
            "nts",
            (
                await data.Collection("event_user_sessions")
                    .Find(new BsonDocument("Id", LegacyTenantData.Uuid(session)))
                    .SingleAsync()
            )["TenantId"].AsString
        );
    }

    [Fact]
    public async Task The_settings_are_dropped_on_apply_and_only_counted_on_a_dry_run()
    {
        var data = await LegacyAsync();
        await data.SettingsAsync(3);

        var dry = await TenantsMigration.Run(data.Database, new TenantsOptions(), NOW);
        Assert.Equal(3, await data.Collection("settings").CountDocumentsAsync(new BsonDocument()));
        Assert.Equal(3, dry.SettingsDocuments);

        var applied = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        Assert.True(applied.SettingsDropped);
        Assert.DoesNotContain("settings", await (await data.Database.ListCollectionNamesAsync()).ToListAsync());
    }

    [Fact]
    public async Task The_indexes_identity_and_the_Tenants_depend_on_are_made_and_the_Api_makes_the_same_ones_over_them()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("ivan@example.test", "Bulgaria");

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        var users = (await (await data.Collection("users").Indexes.ListAsync()).ToListAsync()).Select(x =>
            x["name"].AsString
        );
        Assert.Contains("identity_email_unique", users);
        Assert.Contains("identity_passkey_credential_unique", users);
        var grants = (await (await data.Collection("event_grants").Indexes.ListAsync()).ToListAsync()).Select(x =>
            x["name"].AsString
        );
        Assert.Contains("grants_one_person_in_one_place", grants);
        Assert.Contains("identity_email_unique", report.IndexesMade);
        Assert.Contains("grants_one_person_in_one_place", report.IndexesMade);

        // The host that starts over this database makes the same indexes: nothing it asks for conflicts with what is there.
        var client = new MongoClient(_mongo.ConnectionString);
        var options = Microsoft.Extensions.Options.Options.Create(
            new Not.Identity.NIdentityOptions { Database = data.Database.DatabaseNamespace.DatabaseName }
        );
        await new Not.Identity.Mongo.IdentityIndexInitializer(client, options).StartAsync(CancellationToken.None);
        await new NoTiming.Api.Features.Tenancy.TenancyIndexes(client, options).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_command_makes_every_index_the_hosts_make_when_they_start_and_no_other()
    {
        var made = await LegacyAsync();
        await made.AccountAsync("ivan@example.test", "Bulgaria");
        await TenantsMigration.Run(made.Database, Apply("Staging"), NOW);

        var fresh = NewDatabase();
        var client = new MongoClient(_mongo.ConnectionString);
        var options = Microsoft.Extensions.Options.Options.Create(
            new Not.Identity.NIdentityOptions { Database = fresh.DatabaseNamespace.DatabaseName }
        );
        await new Not.Identity.Mongo.IdentityIndexInitializer(client, options).StartAsync(CancellationToken.None);
        await new NoTiming.Api.Features.Tenancy.TenancyIndexes(client, options).StartAsync(CancellationToken.None);

        Assert.Equal(await IndexNamesAsync(fresh), await IndexNamesAsync(made.Database));
    }

    [Fact]
    public async Task A_country_whose_ISO_code_has_spaces_around_it_is_still_the_country_it_names()
    {
        var data = new LegacyTenantData(NewDatabase());
        await data.CountryAsync("Bulgaria", " BG ");

        var report = await TenantsMigration.Run(data.Database, Apply("Staging"), NOW);

        Assert.True(report.HasBulgaria);
        Assert.False(report.Refused);
        Assert.Equal(["country-bg"], report.TenantsMade);
    }

    [Fact]
    public async Task A_unique_index_that_the_data_cannot_have_stops_the_run_before_the_Tenant_is_stamped()
    {
        var data = await LegacyAsync();
        await data.AccountAsync(
            "one@example.test",
            "Bulgaria",
            x => x["Passkeys"] = new BsonArray { new BsonDocument("CredentialId", "the-same") }
        );
        await data.AccountAsync(
            "two@example.test",
            "Bulgaria",
            x => x["Passkeys"] = new BsonArray { new BsonDocument("CredentialId", "the-same") }
        );
        var club = await data.OwnedAsync("clubs");

        await Assert.ThrowsAnyAsync<MongoException>(() => TenantsMigration.Run(data.Database, Apply("Staging"), NOW));

        Assert.Equal("nts", (await data.OneAsync("clubs", club))["TenantId"].AsString); // the unique indexes come first
    }

    static async Task<SortedDictionary<string, string[]>> IndexNamesAsync(IMongoDatabase database)
    {
        var names = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var collection in await (await database.ListCollectionNamesAsync()).ToListAsync())
        {
            var indexes = await (
                await database.GetCollection<BsonDocument>(collection).Indexes.ListAsync()
            ).ToListAsync();
            var named = indexes.Select(x => x["name"].AsString).Where(x => x != "_id_").Order(StringComparer.Ordinal);
            if (named.Any())
            {
                names[collection] = [.. named];
            }
        }

        return names;
    }

    static (string Tenant, int Accounts)[] PlacedOf(TenantsReport report)
    {
        return [.. report.Placed.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => (x.Key, x.Value))];
    }

    static TenantsOptions Apply(string environment)
    {
        return new TenantsOptions { Apply = true, Environment = environment };
    }

    async Task<LegacyTenantData> LegacyAsync()
    {
        var data = new LegacyTenantData(NewDatabase());
        await data.BulgariaAsync();
        return data;
    }

    IMongoDatabase NewDatabase()
    {
        return new MongoClient(_mongo.ConnectionString).GetDatabase("tenants-" + Guid.NewGuid().ToString("N"));
    }
}
