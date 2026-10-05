using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using Not.Application.DomainEvents;
using Not.Application.HTTP;
using Not.Domain;
using Not.Domain.Abstractions;
using NoTiming.Api.JsonApi;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The repositories of the Ui over the Api (#603, ADR-0008): the JSON:API variant of the client's repository, which the
/// five families that moved to the Api are read and written through, run against the real host with the session of a
/// person signed in. What is asked of the server and what is left to the client is told by what the client asked for; what
/// the Api answers with an error is read with its code and never lost; and what the Api keeps to itself is neither read nor
/// sent. The routes themselves are the other tests' (<c>ClubRoutesTests</c>, <c>ConfigureEventRoutesTests</c>, ...).
/// </summary>
public sealed class JsonApiRepositoryTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

    readonly MongoFixture _mongo;

    public JsonApiRepositoryTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_club_made_through_the_repository_is_the_Tenants_and_is_read_back_by_its_id()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var clubs = new ClubApiRepository(JsonApiClients.Of(api, root, out var requests));
        var id = Guid.NewGuid();

        await clubs.Create(new Club("Sofia Riders", id));
        var read = await clubs.Read(id);

        Assert.Null(clubs.LastError);
        Assert.Equal(id, read!.Id);
        Assert.Equal("Sofia Riders", read.Name);
        Assert.Equal(
            tenant,
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!["TenantId"].AsString
        );
        Assert.Equal(["POST /api/clubs", $"GET /api/clubs/{id}"], requests.Asked);
    }

    [Fact]
    public async Task Every_club_of_the_Tenant_is_read_however_many_pages_they_make()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await RegistrySeed.ClubsAsync(_mongo.ConnectionString, tenant, 1203, "Club");
        await RegistrySeed.ClubsAsync(_mongo.ConnectionString, other, 5, "Foreign");
        var clubs = new ClubApiRepository(JsonApiClients.Of(api, member, out var requests));

        var read = (await clubs.ReadMany()).ToList();

        Assert.Equal(1203, read.Count);
        Assert.Equal(1203, read.Select(x => x.Id).Distinct().Count());
        Assert.All(read, x => Assert.StartsWith("Club ", x.Name));
        Assert.Equal(3, requests.Asked.Count);
        Assert.Contains("page[number]=1", requests.Asked[0]);
        Assert.Contains("page[number]=3", requests.Asked[2]);
    }

    [Fact]
    public async Task A_list_that_cannot_be_read_to_its_end_is_not_returned_in_part_and_says_why()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await RegistrySeed.ClubsAsync(_mongo.ConnectionString, tenant, 1203, "Club");
        var clubs = new ClubApiRepository(JsonApiClients.Of(api, member, out var requests));
        requests.Answer = request =>
            Uri.UnescapeDataString(request.RequestUri!.Query).Contains("page[number]=2", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : null;

        var read = (await clubs.ReadMany()).ToList();

        Assert.Empty(read);
        Assert.Equal(503, clubs.LastError?.Status);
        requests.Answer = null;
        Assert.Equal(1203, (await clubs.ReadMany()).Count());
        Assert.Null(clubs.LastError);
    }

    [Fact]
    public async Task A_filter_that_can_be_written_is_asked_of_the_server_and_one_that_cannot_is_applied_to_what_is_read()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Alpha");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Alpine");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Beta");
        var clubs = new ClubApiRepository(JsonApiClients.Of(api, member, out var requests));

        var byServer = (await clubs.ReadMany(x => x.Name == "Alpha")).ToList();
        var byClient = (await clubs.ReadMany(x => x.Name.StartsWith("Alp"))).ToList();
        var first = await clubs.Read(x => x.Name == "Beta");

        Assert.Equal(["Alpha"], byServer.Select(x => x.Name));
        Assert.Equal(["Alpha", "Alpine"], byClient.Select(x => x.Name).Order());
        Assert.Equal("Beta", first!.Name);
        Assert.Contains("filter=name%20eq%20%27Alpha%27", requests.Asked[0]);
        Assert.DoesNotContain("filter=", requests.Asked[1]);
        Assert.Contains("filter=name%20eq%20%27Beta%27", requests.Asked[2]);
    }

    [Fact]
    public async Task A_club_is_changed_and_removed_through_the_repository()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var clubs = new ClubApiRepository(JsonApiClients.Of(api, root, out var requests));
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Old Name");
        var other = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Another");
        var third = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Third");

        await clubs.Update(new Club("New Name", id));
        await clubs.Delete(other);
        await clubs.DeleteMany(x => x.Name == "Third");

        Assert.Null(clubs.LastError);
        Assert.Equal(
            "New Name",
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!["Name"].AsString
        );
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", other));
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", third));
        Assert.Contains($"PATCH /api/clubs/{id}", requests.Asked);
        Assert.Contains($"DELETE /api/clubs/{other}", requests.Asked);
    }

    [Fact]
    public async Task A_row_that_is_not_there_is_read_as_none_and_neither_it_nor_its_removal_is_an_error()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var theirs = await RegistrySeed.ClubAsync(_mongo.ConnectionString, other, "Theirs");
        var clubs = new ClubApiRepository(JsonApiClients.Of(api, root, out _));

        var missing = await clubs.Read(Guid.NewGuid());
        Assert.Null(clubs.LastError);
        var foreign = await clubs.Read(theirs);
        Assert.Null(clubs.LastError);
        await clubs.Delete(Guid.NewGuid());
        Assert.Null(clubs.LastError);

        Assert.Null(missing);
        Assert.Null(foreign);
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", theirs));
    }

    [Fact]
    public async Task What_the_Api_refuses_is_read_with_its_code_and_what_it_was_asked_to_change_is_left_as_it_was()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Kept");
        var asMember = new ClubApiRepository(JsonApiClients.Of(api, member, out _));
        var asNobody = new ClubApiRepository(JsonApiClients.Of(api, null, out _));

        await asMember.Create(new Club("Refused"));
        var refusedCreate = asMember.LastError;
        await asMember.Update(new Club("Taken", id));
        var refusedUpdate = asMember.LastError;
        await asMember.Delete(id);
        var refusedDelete = asMember.LastError;
        var nothing = (await asNobody.ReadMany()).ToList();
        var notSignedIn = asNobody.LastError;
        await asMember.ReadMany();

        Assert.Equal(403, refusedCreate!.Status);
        Assert.Equal("not-allowed", refusedCreate.Code);
        Assert.Equal("not-allowed", refusedUpdate!.Code);
        Assert.Equal("not-allowed", refusedDelete!.Code);
        Assert.False(string.IsNullOrWhiteSpace(refusedCreate.Message));
        Assert.Empty(nothing);
        Assert.Equal(401, notSignedIn!.Status);
        Assert.Equal("not-signed-in", notSignedIn.Code);
        Assert.Null(asMember.LastError); // the next request that is taken clears what the last said
        Assert.Equal("Kept", (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "clubs", id))!["Name"].AsString);
        Assert.Equal(1, await RegistrySeed.CountAsync(_mongo.ConnectionString, "clubs", tenant));
    }

    [Fact]
    public async Task A_Horse_that_loses_its_optional_members_loses_them_in_the_Api_too()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var horses = new HorseApiRepository(JsonApiClients.Of(api, root, out _));
        var id = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Barza", "Barza", "103AB45");

        await horses.Update(new Horse("Barza", null, null, id));
        var read = await horses.Read(id);

        Assert.Null(horses.LastError);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "horses", id))!;
        Assert.False(stored.Contains("NameEnglish"));
        Assert.False(stored.Contains("FeiId"));
        Assert.Null(read!.NameEnglish);
        Assert.Null(read.FeiId);
    }

    [Fact]
    public async Task The_account_an_Athlete_is_linked_to_is_neither_read_nor_sent_and_an_edit_leaves_the_link_alone()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var athletes = new AthleteApiRepository(JsonApiClients.Of(api, root, out _));
        var country = new Country(Guid.NewGuid(), "Bulgaria", "BG", "BUL", "bg-BG");
        var linked = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Linked Rider",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            user: new BsonDocument
            {
                { "_id", RegistrySeed.Binary(Guid.NewGuid()) },
                { "TenantId", "nts" },
                { "Email", "rider.secret@example.test" },
                { "Name", "Rider Secret" },
            }
        );
        var madeId = Guid.NewGuid();

        var read = await athletes.Read(linked);
        await athletes.Update(new Athlete("Renamed Rider", null, "10012345", country, null, linked));
        await athletes.Create(
            new Athlete(
                "Made Rider",
                null,
                null,
                country,
                new Club("Sofia Riders"),
                madeId,
                new User("made@example.test", "Made")
            )
        );

        Assert.Null(athletes.LastError);
        Assert.Null(read!.User);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", linked))!;
        Assert.Equal("Renamed Rider", stored["Name"].AsString);
        Assert.Equal("rider.secret@example.test", stored["User"]["Email"].AsString);
        var made = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", madeId))!;
        Assert.False(made.Contains("User"));
        Assert.Equal("Sofia Riders", made["Club"]["Name"].AsString);
    }

    [Fact]
    public async Task A_country_is_made_by_the_Developer_and_refused_to_anybody_else_with_the_code_that_says_so()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, home: null, isDeveloper: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var byDeveloper = new CountryApiRepository(JsonApiClients.Of(api, developer, out _));
        var byRoot = new CountryApiRepository(JsonApiClients.Of(api, root, out _));
        var iso = CountrySeed.UniqueIsoCode();
        var id = Guid.NewGuid();

        await byRoot.Create(new Country(Guid.NewGuid(), "Refused " + iso, iso, null, null));
        var refusal = byRoot.LastError;
        await byDeveloper.Create(new Country(id, "Land " + iso, iso, iso[..3], "xx-XX"));
        var read = await byRoot.Read(id);
        var found = (await byRoot.ReadMany(x => x.IsoCode == iso)).ToList();

        Assert.Equal("not-developer", refusal!.Code);
        Assert.Null(byDeveloper.LastError);
        Assert.Equal("Land " + iso, read!.Name);
        Assert.Equal("xx-XX", read.Locale);
        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task A_Setup_with_everything_in_it_is_made_by_the_Tenant_Root_and_read_back_by_the_repository()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var dispatcher = new RecordingDispatcher();
        var setups = new ConfigureEventApiRepository(JsonApiClients.Of(api, root, out var requests), dispatcher);
        var id = Guid.NewGuid();
        var made = SetupFactory.Full(id, "Full Setup");

        await setups.Create(made);
        var read = await setups.Read(id);
        var listed = (await setups.ReadMany()).ToList();

        Assert.Null(setups.LastError);
        var sent = ConfigureEventModel.From(made);
        var received = ConfigureEventModel.From(read!);
        received.TenantId = sent.TenantId; // what the Api sets is the Api's: the Tenant and the Main Operator
        received.MainOperatorId = sent.MainOperatorId;
        Assert.Equal(
            JsonSerializer.Serialize(sent, JsonApiResults.Options),
            JsonSerializer.Serialize(received, JsonApiResults.Options)
        );
        Assert.Equal(id, Assert.Single(listed).Id);
        Assert.Equal(["POST /api/configure-events", $"PATCH /api/configure-events/{id}"], requests.Asked.Take(2));
        Assert.Empty(dispatcher.Events); // a Setup that was made is not a Setup that was updated
        var stored = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal(root.Id, stored["MainOperatorId"].AsGuid);
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Single(stored["Competitions"].AsBsonArray);
    }

    [Fact]
    public async Task A_Setup_that_has_not_started_is_updated_and_announced_and_one_that_has_is_refused_and_not()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var dispatcher = new RecordingDispatcher();
        var setups = new ConfigureEventApiRepository(JsonApiClients.Of(api, mainOperator, out _), dispatcher);
        var open = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Open");
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var openSetup = (await setups.Read(open))!;
        var liveSetup = (await setups.Read(live))!;

        await setups.Update(SetupFactory.Full(open, "Configured"));
        var taken = setups.LastError;
        await setups.Update(SetupFactory.Full(live, "Too late"));
        var refused = setups.LastError;

        Assert.Equal("Open", openSetup.Name);
        Assert.NotNull(liveSetup);
        Assert.Null(taken);
        Assert.Equal("event-started", refused!.Code);
        Assert.Equal(409, refused.Status);
        Assert.Equal(
            open,
            Assert.Single(dispatcher.Events.OfType<NTS.Domain.Setup.Events.ConfigureEventUpdated>()).EventId
        );
        Assert.Equal("Configured", (await EventSeed.SetupOfAsync(_mongo.ConnectionString, open))!["Name"].AsString);
        Assert.StartsWith("Event ", (await EventSeed.SetupOfAsync(_mongo.ConnectionString, live))!["Name"].AsString);
    }

    sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task Dispatch(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }

        public Task Dispatch(Aggregate aggregate, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
