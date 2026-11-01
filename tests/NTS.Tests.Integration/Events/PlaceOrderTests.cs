using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The order in which a request is answered (ADR-0012, #671), tried on the routes whose place is not in the body: an Event
/// named by the route, an Event named by the id of the document, the Tenant the caller acts in, the Event named in the filter
/// of a list, the Event named in the body of a row that is made and the Event a row belongs to. The first thing wrong is the
/// answer: the transport, then the place (400 when it is named wrongly, 404 when it is not there), then what the caller holds,
/// then the stage, then the content. The expectations are written from that order and not from what a route does.
/// </summary>
public sealed class PlaceOrderTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public PlaceOrderTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_change_of_a_Setup_is_answered_by_the_transport_then_the_place_then_the_Level_then_the_stage_then_the_content()
    {
        await using var scene = await SceneAsync();
        var elsewhere = Guid.NewGuid();

        var notJsonApi = await SendRawAsync(
            scene.Member,
            HttpMethod.Patch,
            $"/api/configure-events/{elsewhere}",
            "text/plain",
            "{}"
        );
        var unknown = await ChangeSetupAsync(scene.Member, elsewhere.ToString(), new { name = "x" });
        var notAnId = await ChangeSetupAsync(scene.Member, "not-a-guid", new { name = "x" });
        var tooLittle = await ChangeSetupAsync(
            scene.Member,
            scene.Unstarted.ToString(),
            new { name = "x" },
            id: Guid.NewGuid().ToString()
        );
        var tooLate = await ChangeSetupAsync(
            scene.ConsoleOperator,
            scene.Live.ToString(),
            new { name = "x" },
            id: Guid.NewGuid().ToString()
        );
        var tooLittleAndTooLate = await ChangeSetupAsync(scene.Member, scene.Live.ToString(), new { name = "x" });
        var mismatched = await ChangeSetupAsync(
            scene.ConsoleOperator,
            scene.Unstarted.ToString(),
            new { name = "x" },
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJsonApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAnId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // the Level comes before the document that does not agree with the route
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode); // the stage comes before it too
        Assert.Equal("event-started", await ErrorCodeAsync(tooLate));
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittleAndTooLate)); // and the Level before the stage
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(mismatched));
    }

    [Fact]
    public async Task An_Event_is_started_from_the_Setup_the_document_names_and_the_name_is_answered_before_anything_about_who_asks()
    {
        await using var scene = await SceneAsync();

        var withoutAnId = await StartAsync(scene.Member, null, new { });
        var notAnId = await StartAsync(scene.Member, "not-a-guid", new { });
        var unknown = await StartAsync(scene.Member, Guid.NewGuid().ToString(), new { });
        var tooLittle = await StartAsync(scene.Member, scene.Unstarted.ToString(), new { nothing = "else" });
        var tooLate = await StartAsync(scene.Root, scene.Historic.ToString(), new { nothing = "else" });
        var content = await StartAsync(scene.ConsoleOperator, scene.Unstarted.ToString(), new { nothing = "else" });

        Assert.Equal(HttpStatusCode.BadRequest, withoutAnId.StatusCode);
        Assert.Equal("id-required", await ErrorCodeAsync(withoutAnId));
        Assert.Equal(HttpStatusCode.BadRequest, notAnId.StatusCode);
        Assert.Equal("invalid-id", await ErrorCodeAsync(notAnId));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // who asks before what is asked
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(tooLate));
        Assert.Equal(HttpStatusCode.BadRequest, content.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(content));
    }

    [Fact]
    public async Task An_Event_is_made_in_the_current_Tenant_of_the_account_which_is_looked_for_before_what_it_holds_and_the_content_last()
    {
        await using var scene = await SceneAsync();
        var homeless = await SignedInAsync(scene.Api, scene.Client, _mongo.ConnectionString, home: null);

        var noTenant = await MakeAsync(homeless, new { name = "x", location = "y" });
        var tooLittle = await MakeAsync(scene.Member, new { name = "", location = "" });
        var content = await MakeAsync(scene.Root, new { name = "", location = "Sofia" });
        var made = await MakeAsync(scene.Root, new { name = "x", location = "Sofia" });

        Assert.Equal(HttpStatusCode.Conflict, noTenant.StatusCode);
        Assert.Equal("no-current-tenant", await ErrorCodeAsync(noTenant));
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // a person who may not make one is not told what is wrong with it
        Assert.Equal("not-root", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.BadRequest, content.StatusCode);
        Assert.Equal("invalid-name", await ErrorCodeAsync(content));
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
    }

    [Fact]
    public async Task The_rules_of_a_Tenant_are_changed_in_the_Tenant_the_route_names_by_Root_and_the_content_is_answered_last()
    {
        await using var scene = await SceneAsync();
        var tenant = scene.Tenant;

        var unknown = await ChangeRulesAsync(
            scene.Member,
            "country-nowhere",
            new { regionalRules = new { rankerCode = "a" } }
        );
        var tooLittle = await ChangeRulesAsync(scene.Member, tenant, new { nothing = "else" });
        var mismatched = await ChangeRulesAsync(
            scene.Root,
            tenant,
            new { regionalRules = new { rankerCode = "a" } },
            "country-xx"
        );
        var content = await ChangeRulesAsync(
            scene.Root,
            tenant,
            new { regionalRules = new { onlyAverageLoopSpeed = "yes" } }
        );
        var changed = await ChangeRulesAsync(
            scene.Root,
            tenant,
            new { regionalRules = new { onlyAverageLoopSpeed = true } }
        );

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // who asks before what is asked
        Assert.Equal("not-root", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        Assert.Equal("tenant-id-mismatch", await ErrorCodeAsync(mismatched));
        Assert.Equal(HttpStatusCode.BadRequest, content.StatusCode);
        Assert.Equal("invalid-rules", await ErrorCodeAsync(content));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    }

    [Fact]
    public async Task A_row_of_what_an_Event_keeps_is_changed_and_removed_in_the_Event_of_the_row_which_is_looked_for_before_what_the_caller_holds_and_the_content_last()
    {
        await using var scene = await SceneAsync();
        var live = EventDataRow.Of("officials", scene.Live);
        var ended = EventDataRow.Of("officials", scene.Historic);
        await live.SeedAsync(_mongo.ConnectionString, scene.Tenant);
        await ended.SeedAsync(_mongo.ConnectionString, scene.Tenant);
        var elsewhere = Guid.NewGuid();

        var notJsonApi = await SendRawAsync(
            scene.Member,
            HttpMethod.Patch,
            $"/api/officials/{elsewhere}",
            "text/plain",
            "{}"
        );
        var unknown = await ChangeRowAsync(scene.ConsoleOperator, elsewhere.ToString(), new { nameEnglish = "x" });
        var notAnId = await ChangeRowAsync(scene.ConsoleOperator, "not-a-guid", new { nameEnglish = "x" });
        var removedUnknown = await scene.ConsoleOperator.Page.DeleteAsync($"/api/officials/{elsewhere}");
        var tooLittle = await ChangeRowAsync(
            scene.Member,
            live.Id.ToString(),
            new { nothing = "else" },
            id: Guid.NewGuid().ToString()
        );
        var removedTooLittle = await scene.Member.Page.DeleteAsync($"/api/officials/{live.Id}");
        var tooLate = await ChangeRowAsync(
            scene.ConsoleOperator,
            ended.Id.ToString(),
            new { nothing = "else" },
            id: Guid.NewGuid().ToString()
        );
        var mismatched = await ChangeRowAsync(
            scene.ConsoleOperator,
            live.Id.ToString(),
            new { nothing = "else" },
            id: Guid.NewGuid().ToString()
        );
        var content = await ChangeRowAsync(scene.ConsoleOperator, live.Id.ToString(), new { nothing = "else" });
        var changed = await ChangeRowAsync(scene.ConsoleOperator, live.Id.ToString(), live.Change);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJsonApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAnId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedUnknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // the Level comes before the document that does not agree with the route
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal("not-console-operator", await ErrorCodeAsync(removedTooLittle));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode); // the stage comes before it too
        Assert.Equal("event-ended", await ErrorCodeAsync(tooLate));
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(mismatched));
        Assert.Equal(HttpStatusCode.BadRequest, content.StatusCode); // the members are checked against the row, last
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(content));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    }

    [Fact]
    public async Task A_row_whose_Event_is_gone_is_not_there_to_change_or_to_remove_whoever_asks()
    {
        await using var scene = await SceneAsync();
        var orphan = IntegrationPayloadFactory.Official(Guid.NewGuid(), null, Guid.NewGuid());
        await EventSeed.OfficialAsync(_mongo.ConnectionString, scene.Tenant, orphan);

        var changed = await ChangeRowAsync(scene.Root, orphan.Id.ToString(), new { nameEnglish = "x" });
        var removed = await scene.Root.Page.DeleteAsync($"/api/officials/{orphan.Id}");

        Assert.Equal(HttpStatusCode.NotFound, changed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    [Fact]
    public async Task A_row_is_made_in_the_Event_the_document_names_which_is_looked_for_before_what_the_caller_holds_and_the_content_last()
    {
        await using var scene = await SceneAsync();

        var notJsonApi = await SendRawAsync(scene.Member, HttpMethod.Post, "/api/officials", "text/plain", "{}");
        var unnamed = await MakeRowAsync(
            scene.ConsoleOperator,
            JsonApiAttributes.Without(EventDataRow.Of("officials", scene.Live).Attributes, "eventId")
        );
        var empty = await MakeRowAsync(scene.ConsoleOperator, NamingEvent(scene.Live, Guid.Empty.ToString()));
        var blank = await MakeRowAsync(scene.ConsoleOperator, NamingEvent(scene.Live, "  "));
        var notAnId = await MakeRowAsync(scene.ConsoleOperator, NamingEvent(scene.Live, "not-a-guid"));
        var unknown = await MakeRowAsync(scene.ConsoleOperator, NamingEvent(scene.Live, Guid.NewGuid().ToString()));
        var tooLittle = await MakeRowAsync(
            scene.Member,
            EventDataRow.Of("officials", scene.Live).Attributes,
            id: "not-a-guid"
        );
        var tooLate = await MakeRowAsync(
            scene.ConsoleOperator,
            EventDataRow.Of("officials", scene.Historic).Attributes,
            id: "not-a-guid"
        );
        var twice = EventDataRow.Of("officials", scene.Live).Attributes;
        twice["EventId"] = JsonSerializer.SerializeToElement(scene.Unstarted);
        var namedTwice = await MakeRowAsync(scene.ConsoleOperator, twice);
        var badId = await MakeRowAsync(
            scene.ConsoleOperator,
            EventDataRow.Of("officials", scene.Live).Attributes,
            id: "not-a-guid"
        );
        var content = EventDataRow.Of("officials", scene.Live).Attributes;
        content["nothing"] = JsonSerializer.SerializeToElement("else");
        var badContent = await MakeRowAsync(scene.ConsoleOperator, content, id: Guid.NewGuid().ToString());
        var made = await MakeRowAsync(
            scene.ConsoleOperator,
            EventDataRow.Of("officials", scene.Live).Attributes,
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJsonApi.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
        Assert.Equal("event-required", await ErrorCodeAsync(unnamed));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode); // the empty id names no Event
        Assert.Equal("event-required", await ErrorCodeAsync(empty));
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode); // a name of nothing but spaces names none
        Assert.Equal("event-required", await ErrorCodeAsync(blank));
        Assert.Equal(HttpStatusCode.BadRequest, notAnId.StatusCode);
        Assert.Equal("invalid-event", await ErrorCodeAsync(notAnId));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // the Level comes before the id that is not an id
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode); // and the stage
        Assert.Equal("event-ended", await ErrorCodeAsync(tooLate));
        Assert.Equal(HttpStatusCode.BadRequest, namedTwice.StatusCode); // one Event lets the caller in, so the document names one
        Assert.Equal("malformed-request", await ErrorCodeAsync(namedTwice));
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        Assert.Equal("invalid-id", await ErrorCodeAsync(badId));
        Assert.Equal(HttpStatusCode.BadRequest, badContent.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(badContent));
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
    }

    [Fact]
    public async Task The_people_who_recorded_times_are_asked_for_in_the_Event_the_filter_names_after_the_query_is_found_whole_and_before_what_the_caller_holds()
    {
        await using var scene = await SceneAsync();

        var badSort = await scene.Member.Page.GetAsync(
            $"/api/time-event-actors?filter=eventId eq {Guid.NewGuid()}&sort=name"
        );
        var unnamed = await scene.Member.Page.GetAsync("/api/time-event-actors");
        var unknown = await scene.Member.Page.GetAsync($"/api/time-event-actors?filter=eventId eq {Guid.NewGuid()}");
        var tooLittle = await scene.Member.Page.GetAsync($"/api/time-event-actors?filter=eventId eq {scene.Live}");
        var allowed = await scene.ConsoleOperator.Page.GetAsync(
            $"/api/time-event-actors?filter=eventId eq {scene.Live}"
        );

        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode); // the query is whole before the Event is looked for
        Assert.Equal("invalid-sort", await ErrorCodeAsync(badSort));
        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
        Assert.Equal("invalid-filter", await ErrorCodeAsync(unnamed));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode);
        Assert.Equal("not-allowed", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task A_Snapshot_is_sent_to_the_Event_the_document_names_which_is_looked_for_before_what_the_caller_holds_and_the_content_last()
    {
        await using var scene = await SceneAsync();

        var notJsonApi = await SendRawAsync(scene.Member, HttpMethod.Post, "/api/snapshots", "text/plain", "{}");
        var unnamed = await SendSnapshotAsync(scene.ConsoleOperator, SnapshotOf(null), Guid.NewGuid());
        var named = SnapshotOf(scene.Live);
        named["eventId"] = "not-a-guid";
        var notAnId = await SendSnapshotAsync(scene.ConsoleOperator, named, Guid.NewGuid());
        var unknown = await SendSnapshotAsync(scene.ConsoleOperator, SnapshotOf(Guid.NewGuid()), Guid.NewGuid());
        var tooLittle = await SendSnapshotAsync(scene.Member, SnapshotOf(scene.Live, "Bogus"), null);
        var tooLate = await SendSnapshotAsync(scene.ConsoleOperator, SnapshotOf(scene.Historic, "Bogus"), null);
        var noId = await SendSnapshotAsync(scene.ConsoleOperator, SnapshotOf(scene.Live), null);
        var emptyId = await SendSnapshotAsync(scene.ConsoleOperator, SnapshotOf(scene.Live), Guid.Empty);
        var badKind = await SendSnapshotAsync(scene.ConsoleOperator, SnapshotOf(scene.Live, "Bogus"), Guid.NewGuid());
        var noParticipation = await SendSnapshotAsync(scene.ConsoleOperator, SnapshotOf(scene.Live), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJsonApi.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
        Assert.Equal("event-required", await ErrorCodeAsync(unnamed));
        Assert.Equal(HttpStatusCode.BadRequest, notAnId.StatusCode);
        Assert.Equal("invalid-event", await ErrorCodeAsync(notAnId));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // the Level comes before the kind that is none and the id that is missing
        Assert.Equal("not-allowed", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode); // and the stage
        Assert.Equal("event-ended", await ErrorCodeAsync(tooLate));
        Assert.Equal("id-required", await ErrorCodeAsync(noId));
        Assert.Equal("invalid-id", await ErrorCodeAsync(emptyId));
        Assert.Equal("invalid-snapshot", await ErrorCodeAsync(badKind));
        Assert.Equal(HttpStatusCode.NotFound, noParticipation.StatusCode); // the work is done last
        Assert.Equal("participation-not-found", await ErrorCodeAsync(noParticipation));
    }

    [Fact]
    public async Task A_group_of_Snapshots_is_sent_to_one_Event_found_once_and_an_entry_of_another_is_refused_on_its_own()
    {
        await using var scene = await SceneAsync();
        var many = Enumerable.Range(0, 101).Select(_ => EntryOf(scene.Live)).ToArray();

        var notAList = await SendRawAsync(
            scene.Member,
            HttpMethod.Post,
            "/api/snapshots/actions/send-group",
            ApiSessions.MEDIA_TYPE,
            """{ "data": {} }"""
        );
        var unnamed = await SendGroupAsync(scene.ConsoleOperator, EntryOf(null));
        var unknown = await SendGroupAsync(scene.ConsoleOperator, EntryOf(Guid.NewGuid()));
        var tooLittleAndTooMany = await SendGroupAsync(scene.Member, many);
        var tooMany = await SendGroupAsync(scene.ConsoleOperator, many);
        var full = await SendGroupAsync(scene.ConsoleOperator, [.. many.Take(100)]);
        var mixed = await SendGroupAsync(scene.ConsoleOperator, EntryOf(scene.Live), EntryOf(scene.Historic));

        Assert.Equal(HttpStatusCode.BadRequest, notAList.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(notAList));
        Assert.Equal(HttpStatusCode.BadRequest, unnamed.StatusCode);
        Assert.Equal("event-required", await ErrorCodeAsync(unnamed));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tooLittleAndTooMany.StatusCode); // the Level comes before the size
        Assert.Equal("not-allowed", await ErrorCodeAsync(tooLittleAndTooMany));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("too-many-snapshots", await ErrorCodeAsync(tooMany));
        Assert.Equal(HttpStatusCode.OK, full.StatusCode); // a hundred is as many as a group may hold
        Assert.Equal(HttpStatusCode.OK, mixed.StatusCode);
        var results = (await ApiSessions.ReadJsonAsync(mixed)).GetProperty("meta").GetProperty("results");
        Assert.Equal(404, results[0].GetProperty("status").GetInt32()); // the first names the Event of the group, which has no such start number
        Assert.Equal(409, results[1].GetProperty("status").GetInt32());
        Assert.Equal("event-mismatch", results[1].GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_Update_of_a_Snapshot_is_about_the_Event_of_the_Participation_that_holds_it_which_is_looked_for_before_what_the_caller_holds()
    {
        await using var scene = await SceneAsync();

        var unknown = await UpdateSnapshotAsync(scene.ConsoleOperator, Guid.NewGuid().ToString());
        var notAnId = await UpdateSnapshotAsync(scene.ConsoleOperator, "not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAnId.StatusCode);
    }

    [Fact]
    public async Task A_row_of_the_registry_is_written_in_the_Tenant_the_account_acts_in_which_is_looked_for_before_what_it_holds_and_the_content_last()
    {
        await using var scene = await SceneAsync();
        var homeless = await SignedInAsync(scene.Api, scene.Client, _mongo.ConnectionString, home: null);
        var club = Guid.NewGuid();

        var noTenant = await WriteClubAsync(homeless, HttpMethod.Post, "/api/clubs", new { name = "Sofia Riders" });
        var tooLittle = await WriteClubAsync(
            scene.Member,
            HttpMethod.Post,
            "/api/clubs",
            new { nothing = "else" },
            "not-a-guid"
        );
        var notAnId = await WriteClubAsync(
            scene.ConsoleOperator,
            HttpMethod.Post,
            "/api/clubs",
            new { name = "Sofia Riders" },
            "not-a-guid"
        );
        var content = await WriteClubAsync(
            scene.ConsoleOperator,
            HttpMethod.Post,
            "/api/clubs",
            new { nothing = "else" },
            club.ToString()
        );
        var made = await WriteClubAsync(
            scene.ConsoleOperator,
            HttpMethod.Post,
            "/api/clubs",
            new { name = "Sofia Riders" },
            club.ToString()
        );
        var unknown = await WriteClubAsync(
            scene.ConsoleOperator,
            HttpMethod.Patch,
            $"/api/clubs/{Guid.NewGuid()}",
            new { name = "x" }
        );
        var changedWithoutTheLevel = await WriteClubAsync(
            scene.Member,
            HttpMethod.Patch,
            $"/api/clubs/{Guid.NewGuid()}",
            new { name = "x" },
            Guid.NewGuid().ToString()
        );
        var mismatched = await WriteClubAsync(
            scene.ConsoleOperator,
            HttpMethod.Patch,
            $"/api/clubs/{club}",
            new { name = "x" },
            Guid.NewGuid().ToString()
        );
        var removedWithoutTheLevel = await scene.Member.Page.DeleteAsync($"/api/clubs/{club}");
        var removed = await scene.ConsoleOperator.Page.DeleteAsync($"/api/clubs/{club}");
        var country = await WriteClubAsync(
            scene.Root,
            HttpMethod.Post,
            "/api/countries",
            new { nothing = "else" },
            "not-a-guid"
        );

        Assert.Equal(HttpStatusCode.Conflict, noTenant.StatusCode);
        Assert.Equal("no-current-tenant", await ErrorCodeAsync(noTenant));
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // the Level comes before the id that is none and the member that is not one
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.BadRequest, notAnId.StatusCode);
        Assert.Equal("invalid-id", await ErrorCodeAsync(notAnId));
        Assert.Equal(HttpStatusCode.BadRequest, content.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(content));
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, changedWithoutTheLevel.StatusCode); // before the row is looked for
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        Assert.Equal("id-mismatch", await ErrorCodeAsync(mismatched));
        Assert.Equal(HttpStatusCode.Forbidden, removedWithoutTheLevel.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, country.StatusCode); // the countries are the Developer's, whoever is Root of a Tenant
        Assert.Equal("not-developer", await ErrorCodeAsync(country));
    }

    [Fact]
    public async Task A_search_of_accounts_is_answered_by_the_query_then_who_asks_then_the_length_of_the_words()
    {
        await using var scene = await SceneAsync();
        var homeless = await SignedInAsync(scene.Api, scene.Client, _mongo.ConnectionString, home: null);
        var developer = await SignedInAsync(
            scene.Api,
            scene.Client,
            _mongo.ConnectionString,
            home: null,
            isDeveloper: true
        );

        var notAFilter = await scene.Member.Page.GetAsync("/api/accounts");
        var tooLittle = await SearchAccountsAsync(scene.Member, "a");
        var tooShort = await SearchAccountsAsync(scene.ConsoleOperator, "a");
        var everywhere = await SearchAccountsAsync(scene.Member, "ab", acrossTenants: true);
        var nowhere = await SearchAccountsAsync(homeless, "ab");
        var nowhereButDeveloper = await SearchAccountsAsync(developer, "ab");

        Assert.Equal(HttpStatusCode.BadRequest, notAFilter.StatusCode); // the query is whole before anybody is asked
        Assert.Equal("invalid-filter", await ErrorCodeAsync(notAFilter));
        Assert.Equal(HttpStatusCode.Forbidden, tooLittle.StatusCode); // and who asks before the length of the words
        Assert.Equal("not-console-operator", await ErrorCodeAsync(tooLittle));
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal("search-too-short", await ErrorCodeAsync(tooShort));
        Assert.Equal(HttpStatusCode.Forbidden, everywhere.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nowhere.StatusCode); // an account that acts in no Tenant is asked what the platform asks
        Assert.Equal(HttpStatusCode.OK, nowhereButDeveloper.StatusCode);
    }

    static Task<HttpResponseMessage> WriteClubAsync(
        Person person,
        HttpMethod method,
        string path,
        object attributes,
        string? id = null
    )
    {
        return person.Page.WriteAsync(method, path, path.Split('/')[2], attributes, id: id);
    }

    static Task<HttpResponseMessage> SearchAccountsAsync(Person person, string text, bool acrossTenants = false)
    {
        var filter = Uri.EscapeDataString($"contains(name,'{text}')");
        return person.Page.GetAsync($"/api/accounts{(acrossTenants ? "/all-tenants" : string.Empty)}?filter={filter}");
    }

    static Dictionary<string, object?> SnapshotOf(Guid? eventId, string kind = "Arrive")
    {
        var attributes = new Dictionary<string, object?>
        {
            ["number"] = 1,
            ["kind"] = kind,
            ["time"] = DateTimeOffset.UtcNow,
        };
        if (eventId != null)
        {
            attributes["eventId"] = eventId;
        }

        return attributes;
    }

    static object EntryOf(Guid? eventId)
    {
        return new
        {
            type = "snapshots",
            id = Guid.NewGuid(),
            attributes = SnapshotOf(eventId),
        };
    }

    static Task<HttpResponseMessage> SendSnapshotAsync(Person person, object attributes, Guid? id)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/snapshots", "snapshots", attributes, id: id?.ToString());
    }

    static Task<HttpResponseMessage> SendGroupAsync(Person person, params object[] entries)
    {
        return SendRawAsync(
            person,
            HttpMethod.Post,
            "/api/snapshots/actions/send-group",
            ApiSessions.MEDIA_TYPE,
            JsonSerializer.Serialize(new { data = entries })
        );
    }

    static Task<HttpResponseMessage> UpdateSnapshotAsync(Person person, string snapshot)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/snapshots/{snapshot}",
            "snapshots",
            new { time = DateTimeOffset.UtcNow }
        );
    }

    static Dictionary<string, JsonElement> NamingEvent(Guid eventId, string named)
    {
        var attributes = EventDataRow.Of("officials", eventId).Attributes;
        attributes["eventId"] = JsonSerializer.SerializeToElement(named);
        return attributes;
    }

    static Task<HttpResponseMessage> ChangeRowAsync(Person person, string route, object attributes, string? id = null)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, $"/api/officials/{route}", "officials", attributes, id: id);
    }

    static Task<HttpResponseMessage> MakeRowAsync(Person person, object attributes, string? id = null)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/officials", "officials", attributes, id: id);
    }

    static Task<HttpResponseMessage> ChangeRulesAsync(
        Person person,
        string tenant,
        object attributes,
        string? id = null
    )
    {
        return person.Page.WriteAsync(HttpMethod.Patch, $"/api/tenants/{tenant}", "tenants", attributes, id: id);
    }

    static Task<HttpResponseMessage> ChangeSetupAsync(Person person, string route, object attributes, string? id = null)
    {
        return person.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/configure-events/{route}",
            "configure-events",
            attributes,
            id: id
        );
    }

    static Task<HttpResponseMessage> StartAsync(Person person, string? id, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/events", "events", attributes, id: id);
    }

    static Task<HttpResponseMessage> MakeAsync(Person person, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Post, "/api/configure-events", "configure-events", attributes);
    }

    static Task<HttpResponseMessage> SendRawAsync(
        Person person,
        HttpMethod method,
        string path,
        string contentType,
        string body
    )
    {
        var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8) };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return person.Page.SendAsync(request);
    }

    async Task<Scene> SceneAsync()
    {
        var api = new ApiFactory(_mongo.ConnectionString);
        var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var now = DateTimeOffset.UtcNow;
        return new Scene(
            api,
            client,
            tenant,
            root,
            consoleOperator,
            member,
            await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Not yet"),
            await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, now),
            await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, now)
        );
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class Scene : IAsyncDisposable
    {
        public Scene(
            ApiFactory api,
            HttpClient client,
            string tenant,
            Person root,
            Person consoleOperator,
            Person member,
            Guid unstarted,
            Guid live,
            Guid historic
        )
        {
            Api = api;
            Client = client;
            Tenant = tenant;
            Root = root;
            ConsoleOperator = consoleOperator;
            Member = member;
            Unstarted = unstarted;
            Live = live;
            Historic = historic;
        }

        public ApiFactory Api { get; }
        public HttpClient Client { get; }
        public string Tenant { get; }
        public Person Root { get; }
        public Person ConsoleOperator { get; }
        public Person Member { get; }
        public Guid Unstarted { get; }
        public Guid Live { get; }
        public Guid Historic { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Api.DisposeAsync();
        }
    }
}
