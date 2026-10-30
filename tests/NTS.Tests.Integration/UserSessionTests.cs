using System.Net;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The state a person keeps per Event (#602): the Snapshots they have sent and the ones they have selected. It is the
/// caller's own and nobody else's: the routes list, read, change and delete the records of the caller, a record of
/// another person is as good as missing, and there is no way to ask for everybody's. What is stored has the shape the
/// Functions API stores (PascalCase members, enums as text), so the Event-scoped records that exist read the same.
/// </summary>
public sealed class UserSessionTests : IClassFixture<MongoFixture>
{
    const string COLLECTION = "event_user_sessions";

    readonly MongoFixture _mongo;

    public UserSessionTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_person_has_no_state_for_any_Event_until_they_make_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);

        var list = await ana.Page.GetAsync("/api/user-sessions");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("application/vnd.api+json", list.Content.Headers.ContentType?.MediaType);
        Assert.Empty((await ApiSessions.ReadJsonAsync(list)).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Making_the_state_for_an_Event_stores_it_for_the_caller_in_the_shape_the_Functions_API_uses()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();

        var created = await ana.Page.WriteAsync(
            HttpMethod.Post,
            "/api/user-sessions",
            "user-sessions",
            new { eventId, state = AState() }
        );

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(created)).GetProperty("data");
        var id = resource.GetProperty("id").GetString();
        Assert.Equal("user-sessions", resource.GetProperty("type").GetString());
        Assert.Equal($"/api/user-sessions/{id}", created.Headers.Location?.ToString());
        var attributes = resource.GetProperty("attributes");
        Assert.Equal(eventId.ToString(), attributes.GetProperty("eventId").GetString());
        var history = attributes.GetProperty("state").GetProperty("snapshotHistory")[0];
        Assert.Equal("Present", history.GetProperty("type").GetString());
        Assert.Equal(7, history.GetProperty("entries")[0].GetProperty("number").GetInt32());
        Assert.Equal("FEI", history.GetProperty("entries")[0].GetProperty("ruleset").GetString());
        Assert.False(attributes.TryGetProperty("userIdentifier", out _)); // whose it is is not a member anyone sets

        var stored = Assert.Single(await RecordsAsync(ana.Id));
        Assert.Equal(id, stored["Id"].AsGuid.ToString());
        Assert.Equal(eventId, stored["EventId"].AsGuid);
        Assert.Equal("nts", stored["TenantId"].AsString);
        Assert.Equal(ana.Id.ToString(), stored["UserIdentifier"].AsString);
        var storedHistory = stored["State"]["SnapshotHistory"][0];
        Assert.Equal("Present", storedHistory["Type"].AsString);
        var storedEntry = storedHistory["Entries"][0];
        Assert.Equal(7, storedEntry["Number"].AsInt32);
        Assert.Equal("Rider Seven", storedEntry["Name"].AsString);
        Assert.Equal("FEI", storedEntry["Ruleset"].AsString);
        Assert.Equal("10:15:30.500", storedEntry["Timestamp"].AsString);
        Assert.Equal(12, stored["State"]["SnapshotSelections"][0]["Number"].AsInt32);
    }

    [Fact]
    public async Task Asking_for_the_state_of_an_Event_that_has_one_returns_it_and_never_makes_a_second()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();

        var first = await MakeAsync(ana, eventId, AState());
        var second = await MakeAsync(ana, eventId, null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondDocument = await ApiSessions.ReadJsonAsync(second);
        Assert.Equal(IdOf(await ApiSessions.ReadJsonAsync(first)), IdOf(secondDocument));
        Assert.Single(await RecordsAsync(ana.Id));
        // What was there is what comes back: the second request did not replace it with nothing.
        var state = secondDocument.GetProperty("data").GetProperty("attributes").GetProperty("state");
        Assert.Equal(1, state.GetProperty("snapshotHistory").GetArrayLength());
    }

    [Fact]
    public async Task A_record_that_the_Functions_API_stored_reads_the_same_way()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        var id = Guid.NewGuid();
        await SeedAsync(ana.Id.ToString(), eventId, id);

        var list = await ana.Page.GetAsync($"/api/user-sessions?filter=eventId eq {eventId}");

        var resource = Assert.Single((await ApiSessions.ReadJsonAsync(list)).GetProperty("data").EnumerateArray());
        Assert.Equal(id.ToString(), resource.GetProperty("id").GetString());
        var state = resource.GetProperty("attributes").GetProperty("state");
        var group = state.GetProperty("snapshotHistory")[0];
        Assert.Equal("Arrive", group.GetProperty("type").GetString());
        Assert.Equal("Rider Nine", group.GetProperty("entries")[0].GetProperty("name").GetString());
        Assert.Equal("Regional", group.GetProperty("entries")[0].GetProperty("ruleset").GetString());
        Assert.Equal("09:00:00.000", group.GetProperty("entries")[0].GetProperty("timestamp").GetString());
        Assert.Equal(9, state.GetProperty("snapshotSelections")[0].GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task The_list_is_the_callers_own_and_a_filter_on_the_Event_narrows_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var boris = await SignedInAsync(api, client);
        var anEvent = Guid.NewGuid();
        var another = Guid.NewGuid();
        await MakeAsync(ana, anEvent, AState());
        await MakeAsync(ana, another, AState());
        await MakeAsync(boris, anEvent, AState());

        var all = await ListAsync(ana, "/api/user-sessions");
        var narrowed = await ListAsync(ana, $"/api/user-sessions?filter=eventId eq {another}");
        var borissOwn = await ListAsync(boris, "/api/user-sessions");
        var none = await ListAsync(ana, $"/api/user-sessions?filter=eventId eq {Guid.NewGuid()}");

        Assert.Equal(new[] { anEvent, another }.Order(), all.Order());
        Assert.Equal([another], narrowed);
        Assert.Equal([anEvent], borissOwn);
        Assert.Empty(none);
    }

    [Fact]
    public async Task Nobody_reads_changes_or_deletes_the_state_of_another_person_and_it_is_as_good_as_missing()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var mallory = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        var id = IdOf(await ApiSessions.ReadJsonAsync(await MakeAsync(ana, eventId, AState())));
        var before = Assert.Single(await RecordsAsync(ana.Id));

        var read = await mallory.Page.GetAsync($"/api/user-sessions/{id}");
        var change = await mallory.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/user-sessions/{id}",
            "user-sessions",
            new { state = new { snapshotHistory = Array.Empty<object>(), snapshotSelections = Array.Empty<object>() } }
        );
        var remove = await mallory.Page.DeleteAsync($"/api/user-sessions/{id}");
        var unknown = await mallory.Page.GetAsync($"/api/user-sessions/{Guid.NewGuid()}");

        foreach (var refused in new[] { read, change, remove })
        {
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            Assert.Equal("not-found", await ErrorCodeAsync(refused));
        }

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode); // the same answer as for one that does not exist
        Assert.Equal(before, Assert.Single(await RecordsAsync(ana.Id)));
        Assert.Empty(await RecordsAsync(mallory.Id));
        Assert.Equal(HttpStatusCode.OK, (await ana.Page.GetAsync($"/api/user-sessions/{id}")).StatusCode);
    }

    [Fact]
    public async Task A_filter_cannot_name_the_owner_so_there_is_no_way_to_ask_for_somebody_elses()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var mallory = await SignedInAsync(api, client);
        await MakeAsync(ana, Guid.NewGuid(), AState());

        var byOwner = await mallory.Page.GetAsync($"/api/user-sessions?filter=userIdentifier eq {ana.Id}");
        var byOwnerCased = await mallory.Page.GetAsync($"/api/user-sessions?filter=UserIdentifier eq '{ana.Id}'");
        var either = await mallory.Page.GetAsync($"/api/user-sessions?filter=eventId eq {Guid.NewGuid()} or 1 eq 1");

        foreach (var refused in new[] { byOwner, byOwnerCased, either })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("invalid-filter", await ErrorCodeAsync(refused));
        }
    }

    [Theory]
    [InlineData("?sort=eventId")]
    [InlineData("?page[size]=10")]
    [InlineData("?include=owner")]
    [InlineData("?fields[user-sessions]=eventId")]
    [InlineData("?something=1")]
    public async Task A_parameter_that_nothing_needs_yet_is_refused(string query)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);

        var refused = await ana.Page.GetAsync("/api/user-sessions" + query);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("unsupported-parameter", await ErrorCodeAsync(refused));
    }

    [Theory]
    [InlineData("eventId not a guid")]
    [InlineData("eventId empty")]
    [InlineData("eventId missing")]
    public async Task A_record_for_something_that_is_not_an_Event_id_is_refused(string kind)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        object attributes = kind switch
        {
            "eventId not a guid" => new { eventId = "seven" },
            "eventId empty" => new { eventId = Guid.Empty },
            _ => new { state = AState() },
        };

        var refused = await ana.Page.WriteAsync(HttpMethod.Post, "/api/user-sessions", "user-sessions", attributes);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("invalid-event", await ErrorCodeAsync(refused));
        Assert.Empty(await RecordsAsync(ana.Id));
    }

    [Fact]
    public async Task The_id_of_a_record_is_made_by_the_server_and_a_document_that_brings_one_is_refused()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);

        var refused = await WriteRawAsync(
            ana,
            HttpMethod.Post,
            "/api/user-sessions",
            JsonSerializer.Serialize(
                new
                {
                    data = new
                    {
                        type = "user-sessions",
                        id = Guid.NewGuid(),
                        attributes = new { eventId = Guid.NewGuid() },
                    },
                }
            )
        );

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("client-id-not-supported", await ErrorCodeAsync(refused));
        Assert.Empty(await RecordsAsync(ana.Id));
    }

    [Theory]
    [InlineData("""{"eventId":"{EVENT}","state":7}""")]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotHistory":[{"type":"Sunday","entries":[]}]}}""")]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotSelections":[{"number":"seven","name":"x"}]}}""")]
    public async Task A_state_that_is_not_a_state_is_refused_as_malformed_and_nothing_is_stored(string attributes)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var body =
            """{"data":{"type":"user-sessions","attributes":"""
            + attributes.Replace("{EVENT}", Guid.NewGuid().ToString())
            + "}}";

        var refused = await WriteRawAsync(ana, HttpMethod.Post, "/api/user-sessions", body);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(refused));
        Assert.Empty(await RecordsAsync(ana.Id));
    }

    [Theory]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotHistory":null}}""")]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotHistory":[null]}}""")]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotHistory":[{"type":"Present","entries":null}]}}""")]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotHistory":[{"type":"Present","entries":[null]}]}}""")]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotSelections":null}}""")]
    [InlineData("""{"eventId":"{EVENT}","state":{"snapshotSelections":[null]}}""")]
    public async Task A_state_with_a_hole_in_it_is_refused_as_not_valid_and_nothing_is_stored(string attributes)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var body =
            """{"data":{"type":"user-sessions","attributes":"""
            + attributes.Replace("{EVENT}", Guid.NewGuid().ToString())
            + "}}";

        var refused = await WriteRawAsync(ana, HttpMethod.Post, "/api/user-sessions", body);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("invalid-state", await ErrorCodeAsync(refused));
        Assert.Empty(await RecordsAsync(ana.Id));
    }

    [Fact]
    public async Task A_change_to_a_state_with_a_hole_in_it_is_refused_and_the_state_stays_as_it_was()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var id = IdOf(await ApiSessions.ReadJsonAsync(await MakeAsync(ana, Guid.NewGuid(), AState())));
        var before = Assert.Single(await RecordsAsync(ana.Id));

        var refused = await WriteRawAsync(
            ana,
            HttpMethod.Patch,
            $"/api/user-sessions/{id}",
            """{"data":{"type":"user-sessions","attributes":{"state":{"snapshotHistory":[null]}}}}"""
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("invalid-state", await ErrorCodeAsync(refused));
        Assert.Equal(before, Assert.Single(await RecordsAsync(ana.Id)));
    }

    [Fact]
    public async Task A_record_that_cannot_be_addressed_is_left_out_of_the_list_and_never_breaks_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        await MakeAsync(ana, eventId, AState());
        // The Functions API leaves out a member that holds the default: a record with the id of nothing has no Id at all.
        await Records()
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", ObjectId.GenerateNewId() },
                    { "TenantId", "nts" },
                    { "EventId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                    { "UserIdentifier", ana.Id.ToString() },
                }
            );

        var list = await ListAsync(ana, "/api/user-sessions");

        Assert.Equal([eventId], list);
    }

    [Fact]
    public async Task A_record_without_an_id_for_the_Event_does_not_stop_the_caller_from_making_theirs()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        await Records()
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", ObjectId.GenerateNewId() },
                    { "TenantId", "nts" },
                    { "EventId", new BsonBinaryData(eventId, GuidRepresentation.Standard) },
                    { "UserIdentifier", ana.Id.ToString() },
                }
            );

        var made = await MakeAsync(ana, eventId, AState());

        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.Equal([eventId], await ListAsync(ana, "/api/user-sessions"));
        Assert.Equal(2, (await RecordsAsync(ana.Id)).Count); // the one nothing can ask for is left where it was
    }

    [Fact]
    public async Task Changing_the_state_replaces_it_and_leaves_the_Event_and_the_owner_alone()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        var id = IdOf(await ApiSessions.ReadJsonAsync(await MakeAsync(ana, eventId, AState())));
        var before = Assert.Single(await RecordsAsync(ana.Id));

        var changed = await ana.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/user-sessions/{id}",
            "user-sessions",
            new
            {
                state = new
                {
                    snapshotHistory = Array.Empty<object>(),
                    snapshotSelections = new[] { new { number = 21, name = "Rider Twenty-One" } },
                },
            }
        );
        var moved = await ana.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/user-sessions/{id}",
            "user-sessions",
            new { eventId = Guid.NewGuid() }
        );

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var after = Assert.Single(await RecordsAsync(ana.Id));
        Assert.Equal(before["_id"], after["_id"]);
        Assert.Equal(before["Id"], after["Id"]);
        Assert.Equal(before["EventId"], after["EventId"]);
        Assert.Equal(before["UserIdentifier"], after["UserIdentifier"]);
        Assert.Empty(after["State"]["SnapshotHistory"].AsBsonArray);
        Assert.Equal(21, after["State"]["SnapshotSelections"][0]["Number"].AsInt32);
        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.Equal("event-immutable", await ErrorCodeAsync(moved));
    }

    [Fact]
    public async Task Deleting_the_state_removes_it_and_a_second_delete_finds_nothing()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var id = IdOf(await ApiSessions.ReadJsonAsync(await MakeAsync(ana, Guid.NewGuid(), AState())));

        var removed = await ana.Page.DeleteAsync($"/api/user-sessions/{id}");
        var read = await ana.Page.GetAsync($"/api/user-sessions/{id}");
        var again = await ana.Page.DeleteAsync($"/api/user-sessions/{id}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Empty(await RecordsAsync(ana.Id));
    }

    [Fact]
    public async Task A_body_of_more_than_a_megabyte_is_refused()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var ana = await SignedInAsync(api, client);
        var big = new string('x', 1_100_000);

        var refused = await WriteRawAsync(
            ana,
            HttpMethod.Post,
            "/api/user-sessions",
            JsonSerializer.Serialize(
                new
                {
                    data = new
                    {
                        type = "user-sessions",
                        attributes = new
                        {
                            eventId = Guid.NewGuid(),
                            state = new { snapshotSelections = new[] { new { number = 1, name = big } } },
                        },
                    },
                }
            )
        );

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.StatusCode);
        Assert.Empty(await RecordsAsync(ana.Id));
    }

    static object AState()
    {
        return new
        {
            snapshotHistory = new[]
            {
                new
                {
                    type = "Present",
                    entries = new[]
                    {
                        new
                        {
                            number = 7,
                            name = "Rider Seven",
                            nameEnglish = "Rider Seven",
                            ruleset = "FEI",
                            timestamp = "10:15:30.500",
                        },
                    },
                },
            },
            snapshotSelections = new[] { new { number = 12, name = "Rider Twelve" } },
        };
    }

    static Guid IdOf(JsonElement document)
    {
        return Guid.Parse(document.GetProperty("data").GetProperty("id").GetString()!);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    static Task<HttpResponseMessage> MakeAsync(Person person, Guid eventId, object? state)
    {
        return person.Page.WriteAsync(
            HttpMethod.Post,
            "/api/user-sessions",
            "user-sessions",
            state == null ? new { eventId } : new { eventId, state }
        );
    }

    static Task<HttpResponseMessage> WriteRawAsync(Person person, HttpMethod method, string path, string json)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, ApiSessions.MEDIA_TYPE),
        };
        return person.Page.SendAsync(request);
    }

    static async Task<List<Guid>> ListAsync(Person person, string path)
    {
        var response = await person.Page.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        return
        [
            .. data.EnumerateArray()
                .Select(x => Guid.Parse(x.GetProperty("attributes").GetProperty("eventId").GetString()!)),
        ];
    }

    IMongoCollection<BsonDocument> Records()
    {
        return new MongoClient(_mongo.ConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>(COLLECTION);
    }

    async Task<List<BsonDocument>> RecordsAsync(Guid owner)
    {
        return await Records().Find(new BsonDocument("UserIdentifier", owner.ToString())).ToListAsync();
    }

    /// <summary>A record as the Functions API stores it: PascalCase members, enums as text, no member that is null.</summary>
    async Task SeedAsync(string owner, Guid eventId, Guid id)
    {
        await Records()
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", ObjectId.GenerateNewId() },
                    { "Id", new BsonBinaryData(id, GuidRepresentation.Standard) },
                    { "TenantId", "nts" },
                    { "EventId", new BsonBinaryData(eventId, GuidRepresentation.Standard) },
                    { "UserIdentifier", owner },
                    {
                        "State",
                        new BsonDocument
                        {
                            {
                                "SnapshotHistory",
                                new BsonArray
                                {
                                    new BsonDocument
                                    {
                                        {
                                            "Entries",
                                            new BsonArray
                                            {
                                                new BsonDocument
                                                {
                                                    { "Number", 9 },
                                                    { "Name", "Rider Nine" },
                                                    { "Ruleset", "Regional" },
                                                    { "Timestamp", "09:00:00.000" },
                                                },
                                            }
                                        },
                                        { "Type", "Arrive" },
                                    },
                                }
                            },
                            {
                                "SnapshotSelections",
                                new BsonArray
                                {
                                    new BsonDocument { { "Number", 9 }, { "Name", "Rider Nine" } },
                                }
                            },
                        }
                    },
                }
            );
    }

    async Task<Person> SignedInAsync(ApiFactory api, HttpClient client)
    {
        var email = UserSeed.NewEmail("session-state");
        var id = await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        var page = new PageClient(client);
        page.Set(await ApiSessions.SignInAsync(api, client, email));
        return new Person(id, page);
    }

    sealed class Person
    {
        public Person(Guid id, PageClient page)
        {
            Id = id;
            Page = page;
        }

        public Guid Id { get; }
        public PageClient Page { get; }
    }
}
