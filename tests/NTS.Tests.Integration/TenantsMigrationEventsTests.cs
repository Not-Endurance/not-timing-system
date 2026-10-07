using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Events;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.Tenants;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0012, #607, what <c>migrate-tenants</c> does for the Events: every Event that is not yet Historic gets its Tenant Root
/// as its Main Operator, an Official or an Operator that was linked by email becomes a grant or a pending invitation, and
/// the state a person kept for an Event is found again by the account that person is. Over documents in the shape the code
/// before stored them, on a MongoDB in a container, with the clock given by the test.
/// </summary>
public sealed class TenantsMigrationEventsTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2031, 3, 14, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public TenantsMigrationEventsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_Event_that_is_not_Historic_gets_the_Tenant_Root_as_Main_Operator_and_a_Historic_Event_gets_none()
    {
        var data = await LegacyAsync();
        var root = await TenantRootAsync(data, "root@example.test");
        var unstarted = await data.SetupAsync();
        var live = await data.SetupAsync();
        await data.StartedAsync(live, NOW.AddDays(2));
        var over = await data.SetupAsync();
        await data.StartedAsync(over, NOW.AddDays(-1));

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal(root, MainOperatorOf(await data.OneAsync("configure_events", unstarted)));
        Assert.Equal(root, MainOperatorOf(await data.OneAsync("configure_events", live)));
        Assert.Equal(root, MainOperatorOf(await data.OneAsync("event_informations", live)));
        Assert.Null(MainOperatorOf(await data.OneAsync("configure_events", over)));
        Assert.Null(MainOperatorOf(await data.OneAsync("event_informations", over)));
        Assert.Equal(2, report.EventsNotHistoric);
        Assert.Equal(2, report.MainOperatorsAssigned);
        Assert.Empty(report.EventsWaitingForMainOperator);
    }

    [Fact]
    public async Task A_deleted_Event_is_asked_nothing_of_and_gets_neither_a_Main_Operator_nor_grants()
    {
        var data = await LegacyAsync();
        await TenantRootAsync(data, "root@example.test");
        await data.AccountAsync("steward@example.test", "Bulgaria");
        var deleted = await data.SetupAsync(officials: [("steward@example.test", "Steward")]);
        await data.StartedAsync(deleted, NOW.AddDays(2));
        await data.Collection("event_informations")
            .UpdateOneAsync(
                new BsonDocument("_id", LegacyTenantData.Uuid(deleted)),
                Builders<BsonDocument>.Update.Set("IsDeleted", true)
            );

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal(0, report.EventsNotHistoric);
        Assert.Equal(0, report.MainOperatorsAssigned);
        Assert.Null(MainOperatorOf(await data.OneAsync("configure_events", deleted)));
        Assert.Null(MainOperatorOf(await data.OneAsync("event_informations", deleted)));
        Assert.Empty(await data.ManyAsync("event_grants"));
    }

    [Fact]
    public async Task An_Event_that_has_a_Main_Operator_keeps_it()
    {
        var data = await LegacyAsync();
        await TenantRootAsync(data, "root@example.test");
        var someone = Guid.NewGuid();
        var event_ = await data.SetupAsync(mainOperator: someone);
        await data.StartedAsync(event_, NOW.AddDays(2), mainOperator: someone);

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal(someone, MainOperatorOf(await data.OneAsync("configure_events", event_)));
        Assert.Equal(someone, MainOperatorOf(await data.OneAsync("event_informations", event_)));
        Assert.Equal(0, report.MainOperatorsAssigned);
    }

    [Fact]
    public async Task An_account_that_is_named_does_not_replace_the_Main_Operator_an_Event_has()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("plain@example.test", "Bulgaria");
        var someone = Guid.NewGuid();
        var event_ = await data.SetupAsync(mainOperator: someone);
        await data.StartedAsync(event_, NOW.AddDays(2), mainOperator: someone);

        await TenantsMigration.Run(
            data.Database,
            new TenantsOptions
            {
                Apply = true,
                Environment = "Staging",
                MainOperatorEmail = "plain@example.test",
            },
            NOW
        );

        Assert.Equal(someone, MainOperatorOf(await data.OneAsync("configure_events", event_)));
        Assert.Equal(someone, MainOperatorOf(await data.OneAsync("event_informations", event_)));
    }

    [Fact]
    public async Task A_Tenant_Root_of_another_Tenant_is_not_the_Main_Operator_of_an_Event_of_this_one()
    {
        var data = await LegacyAsync();
        await data.CountryAsync("Turkey", "TR");
        var turkish = await data.AccountAsync("turkish@example.test", "Turkey");
        await data.TenantRootAsync(turkish, "country-tr");
        var waiting = await data.SetupAsync();

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal([waiting], report.EventsWaitingForMainOperator);
        Assert.Null(MainOperatorOf(await data.OneAsync("configure_events", waiting)));
    }

    [Fact]
    public async Task An_Event_that_has_a_Main_Operator_on_one_of_its_documents_gets_it_on_the_other_too()
    {
        var data = await LegacyAsync();
        await TenantRootAsync(data, "root@example.test");
        var someone = Guid.NewGuid();
        var event_ = await data.SetupAsync(mainOperator: someone);
        await data.StartedAsync(event_, NOW.AddDays(2));

        await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal(someone, MainOperatorOf(await data.OneAsync("event_informations", event_)));
    }

    [Fact]
    public async Task With_no_Tenant_Root_nobody_is_assigned_and_the_Events_wait_for_a_second_run_after_one_is_seeded()
    {
        var data = await LegacyAsync();
        var account = await data.AccountAsync("root@example.test", "Bulgaria");
        var waiting = await data.SetupAsync();

        var first = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.False(first.Refused);
        Assert.Equal([waiting], first.EventsWaitingForMainOperator);
        Assert.Null(MainOperatorOf(await data.OneAsync("configure_events", waiting)));
        Assert.Equal("country-bg", (await data.OneAsync("configure_events", waiting))["TenantId"].AsString); // the rest was done
        var text = new StringWriter();
        first.WriteTo(text);
        Assert.Contains("no Tenant Root", text.ToString());

        await data.TenantRootAsync(account);
        var second = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal(account, MainOperatorOf(await data.OneAsync("configure_events", waiting)));
        Assert.Equal(1, second.MainOperatorsAssigned);
        Assert.Empty(second.EventsWaitingForMainOperator);
    }

    [Fact]
    public async Task Without_a_Tenant_Root_the_Events_wait_again_on_a_second_run_though_every_account_has_a_Membership_now()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("one@example.test", "Bulgaria");
        await data.AccountAsync("two@example.test", "Bulgaria");
        var waiting = await data.SetupAsync();
        await TenantsMigration.Run(data.Database, Apply(), NOW);

        var again = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal([waiting], again.EventsWaitingForMainOperator);
        Assert.Null(MainOperatorOf(await data.OneAsync("configure_events", waiting)));
    }

    [Fact]
    public async Task With_several_Tenant_Roots_the_command_asks_which_and_uses_the_account_it_is_given()
    {
        var data = await LegacyAsync();
        var one = await TenantRootAsync(data, "one@example.test");
        var two = await TenantRootAsync(data, "two@example.test");
        var waiting = await data.SetupAsync();

        var asked = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal([waiting], asked.EventsWaitingForMainOperator);
        Assert.Contains("--main-operator", asked.MainOperatorNote);
        Assert.Null(MainOperatorOf(await data.OneAsync("configure_events", waiting)));

        var chosen = await TenantsMigration.Run(
            data.Database,
            new TenantsOptions
            {
                Apply = true,
                Environment = "Staging",
                MainOperatorEmail = " Two@Example.Test ",
            },
            NOW
        );

        Assert.Equal(two, MainOperatorOf(await data.OneAsync("configure_events", waiting)));
        Assert.NotEqual(one, two);
        Assert.Equal(1, chosen.MainOperatorsAssigned);
    }

    [Fact]
    public async Task The_account_named_as_Main_Operator_need_not_be_a_Tenant_Root_but_has_to_exist()
    {
        var data = await LegacyAsync();
        var plain = await data.AccountAsync("plain@example.test", "Bulgaria");
        var waiting = await data.SetupAsync();

        var unknown = await TenantsMigration.Run(
            data.Database,
            new TenantsOptions
            {
                Apply = true,
                Environment = "Staging",
                MainOperatorEmail = "nobody@example.test",
            },
            NOW
        );
        Assert.True(unknown.Refused);
        Assert.Contains(unknown.Refusals, x => x.Contains("--main-operator"));
        Assert.Null(MainOperatorOf(await data.OneAsync("configure_events", waiting)));

        await TenantsMigration.Run(
            data.Database,
            new TenantsOptions
            {
                Apply = true,
                Environment = "Staging",
                MainOperatorEmail = "plain@example.test",
            },
            NOW
        );

        Assert.Equal(plain, MainOperatorOf(await data.OneAsync("configure_events", waiting)));
    }

    [Fact]
    public async Task An_Event_whose_id_is_not_a_standard_UUID_waits_for_a_run_after_the_ids_are_converted()
    {
        var data = await LegacyAsync();
        await TenantRootAsync(data, "root@example.test");
        await data.Collection("configure_events")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", 12 },
                    { "TenantId", "nts" },
                    { "Name", "Old Event" },
                }
            );
        await data.Collection("event_informations")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", 12 },
                    { "TenantId", "nts" },
                    { "EndDay", NOW.AddDays(2).UtcDateTime },
                }
            );

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal(2, report.EventsWithOtherIds);
        Assert.Equal(0, report.EventsNotHistoric);
        Assert.False(
            (await data.Collection("configure_events").Find(new BsonDocument("_id", 12)).SingleAsync()).Contains(
                "MainOperatorId"
            )
        );
        Assert.Empty(await data.ManyAsync("event_grants"));
        Assert.Equal(
            "country-bg",
            (await data.Collection("configure_events").Find(new BsonDocument("_id", 12)).SingleAsync())[
                "TenantId"
            ].AsString
        ); // what does not need an id is done
    }

    [Fact]
    public async Task A_started_Event_whose_end_is_not_a_date_is_taken_to_be_over_and_is_listed()
    {
        var data = await LegacyAsync();
        await TenantRootAsync(data, "root@example.test");
        var event_ = await data.SetupAsync();
        await data.StartedAsync(event_, NOW.AddDays(2));
        await data.Collection("event_informations")
            .UpdateOneAsync(
                new BsonDocument("_id", LegacyTenantData.Uuid(event_)),
                Builders<BsonDocument>.Update.Set("EndDay", "2031-03-16")
            );

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal([event_], report.EventsWithoutAnEnd);
        Assert.Null(MainOperatorOf(await data.OneAsync("event_informations", event_)));
        Assert.False(report.Refused);
    }

    [Fact]
    public async Task The_Officials_and_Operators_a_Setup_names_become_grants_for_accounts_and_pending_invitations()
    {
        var data = await LegacyAsync();
        var steward = await data.AccountAsync("steward@example.test", "Bulgaria");
        var operatorAccount = await data.AccountAsync("operator@example.test", "Bulgaria");
        var setup = await data.SetupAsync(
            officials: [(" Steward@Example.Test ", "Steward"), ("new@example.test", "ChiefSteward")],
            operators: ["Operator@Example.Test"]
        );

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        var grants = (await data.ManyAsync("event_grants"))
            .Select(GrantDocuments.ToGrant)
            .OfType<EventGrant>()
            .ToList();
        Assert.Equal(3, grants.Count);
        Assert.All(grants, x => Assert.Equal(("country-bg", setup), (x.TenantId, x.EventId)));
        var official = Assert.Single(grants, x => x.Email == "steward@example.test");
        Assert.Equal(
            (GrantKind.Official, OfficialRole.Steward, steward),
            (official.Kind, official.OfficialRole, official.AccountId)
        );
        var invited = Assert.Single(grants, x => x.Email == "new@example.test");
        Assert.Equal((GrantKind.Official, OfficialRole.ChiefSteward), (invited.Kind, invited.OfficialRole));
        Assert.True(invited.IsPending);
        var operator_ = Assert.Single(grants, x => x.Kind == GrantKind.Operator);
        Assert.Equal((operatorAccount, (OfficialRole?)null), (operator_.AccountId, operator_.OfficialRole));
        Assert.Equal(3, report.GrantsMade);
        Assert.Equal(1, report.GrantsPending);
        Assert.Equal(
            ["new@example.test", "operator@example.test", "steward@example.test"],
            (await data.ManyAsync("event_grants")).Select(x => x["Email"].AsString).Order(StringComparer.Ordinal)
        ); // as they are stored, in the form the Api matches them by
    }

    [Fact]
    public async Task A_person_who_is_an_Official_in_two_roles_and_an_Operator_gets_a_grant_for_each()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("both@example.test", "Bulgaria");
        await data.SetupAsync(
            officials: [("both@example.test", "Steward"), ("both@example.test", "GroundJury")],
            operators: ["both@example.test"]
        );

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        var grants = (await data.ManyAsync("event_grants"))
            .Select(GrantDocuments.ToGrant)
            .OfType<EventGrant>()
            .ToList();
        Assert.Equal(3, grants.Count);
        Assert.Contains(grants, x => x is { Kind: GrantKind.Official, OfficialRole: OfficialRole.Steward });
        Assert.Contains(grants, x => x is { Kind: GrantKind.Official, OfficialRole: OfficialRole.GroundJury });
        Assert.Contains(grants, x => x is { Kind: GrantKind.Operator });
        Assert.Equal(3, report.GrantsMade);
    }

    [Fact]
    public async Task The_copies_a_started_Event_made_are_linked_through_the_account_they_name()
    {
        var data = await LegacyAsync();
        var official = await data.AccountAsync("official@example.test", "Bulgaria");
        var operatorAccount = await data.AccountAsync("operator@example.test", "Bulgaria");
        var deleted = await data.AccountAsync("deleted@example.test", "Bulgaria");
        var event_ = await data.SetupAsync();
        await data.StartedAsync(event_, NOW.AddDays(2));
        await data.OfficialCopyAsync(event_, official, "GroundJury");
        await data.OperatorCopyAsync(event_, operatorAccount);
        await data.OfficialCopyAsync(event_, deleted, "Steward", deleted: true);
        await data.OfficialCopyAsync(event_, null, "Steward"); // an Official that is not linked to anybody
        await data.OfficialCopyAsync(event_, Guid.NewGuid(), "Steward"); // a user that is not there any more

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        var grants = (await data.ManyAsync("event_grants"))
            .Select(GrantDocuments.ToGrant)
            .OfType<EventGrant>()
            .ToList();
        Assert.Equal(2, grants.Count);
        Assert.Contains(
            grants,
            x => x is { Kind: GrantKind.Official, OfficialRole: OfficialRole.GroundJury } && x.AccountId == official
        );
        Assert.Contains(grants, x => x is { Kind: GrantKind.Operator } && x.AccountId == operatorAccount);
        Assert.Equal(1, report.GrantsDangling);
    }

    [Fact]
    public async Task A_Setup_and_the_copies_of_its_Event_that_name_the_same_person_make_one_grant()
    {
        var data = await LegacyAsync();
        var steward = await data.AccountAsync("steward@example.test", "Bulgaria");
        var event_ = await data.SetupAsync(officials: [("steward@example.test", "Steward")]);
        await data.StartedAsync(event_, NOW.AddDays(2));
        await data.OfficialCopyAsync(event_, steward, "Steward");

        await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Single(await data.ManyAsync("event_grants"));
    }

    [Fact]
    public async Task The_grants_of_a_Historic_Event_are_not_made()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("steward@example.test", "Bulgaria");
        var over = await data.SetupAsync(officials: [("steward@example.test", "Steward")]);
        await data.StartedAsync(over, NOW.AddDays(-1));

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Empty(await data.ManyAsync("event_grants"));
        Assert.Equal(0, report.GrantsMade);
    }

    [Fact]
    public async Task Running_it_again_makes_no_grant_twice_and_leaves_the_ones_an_account_got_since()
    {
        var data = await LegacyAsync();
        var event_ = await data.SetupAsync(officials: [("new@example.test", "Steward")]);
        var options = Apply();
        await TenantsMigration.Run(data.Database, options, NOW);
        var invitation = Assert.Single(await data.ManyAsync("event_grants"));
        var account = await data.AccountAsync("new@example.test", "Bulgaria");
        await data.Collection("event_grants")
            .UpdateOneAsync(
                new BsonDocument("_id", invitation["_id"]),
                Builders<BsonDocument>.Update.Set("AccountId", LegacyTenantData.Uuid(account))
            ); // the person registered and the Api attached the invitation

        var again = await TenantsMigration.Run(data.Database, options, NOW);

        var grant = Assert.Single(await data.ManyAsync("event_grants"));
        Assert.Equal(account, grant["AccountId"].AsGuid);
        Assert.Equal(0, again.GrantsMade);
        Assert.Equal(event_, grant["EventId"].AsGuid);
    }

    [Fact]
    public async Task The_state_kept_under_an_email_is_found_again_under_the_account_and_the_rest_is_counted()
    {
        var data = await LegacyAsync();
        var account = await data.AccountAsync("ivan@example.test", "Bulgaria");
        var event_ = Guid.NewGuid();
        var byEmail = await data.SessionAsync(" Ivan@Example.Test ", event_);
        var already = await data.SessionAsync(account.ToString(), event_);
        var byOid = await data.SessionAsync("0f8fad5b-d9cb-469f-a165-70867728950e", event_);

        var report = await TenantsMigration.Run(data.Database, Apply(), NOW);

        Assert.Equal(account.ToString(), await OwnerOfAsync(data, byEmail));
        Assert.Equal(account.ToString(), await OwnerOfAsync(data, already));
        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", await OwnerOfAsync(data, byOid)); // nobody can be told from it
        Assert.Equal(1, report.SessionsRekeyed);
        Assert.Equal(2, report.SessionsKept);
    }

    static async Task<string> OwnerOfAsync(LegacyTenantData data, Guid session)
    {
        var record = await data.Collection("event_user_sessions")
            .Find(new BsonDocument("Id", LegacyTenantData.Uuid(session)))
            .SingleAsync();
        return record["UserIdentifier"].AsString;
    }

    static Guid? MainOperatorOf(BsonDocument document)
    {
        return document.TryGetValue("MainOperatorId", out var id) ? id.AsGuid : null;
    }

    static async Task<Guid> TenantRootAsync(LegacyTenantData data, string email)
    {
        var account = await data.AccountAsync(email, "Bulgaria");
        await data.TenantRootAsync(account);
        return account;
    }

    static TenantsOptions Apply()
    {
        return new TenantsOptions { Apply = true, Environment = "Staging" };
    }

    async Task<LegacyTenantData> LegacyAsync()
    {
        var data = new LegacyTenantData(
            new MongoClient(_mongo.ConnectionString).GetDatabase("tenants-" + Guid.NewGuid().ToString("N"))
        );
        await data.BulgariaAsync();
        return data;
    }
}
