using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The logos that go at the head of what an Event prints (#646, ADR-0012): <c>/api/tenants/{id}/branding</c> and
/// <c>/api/events/{id}/branding</c>. A Tenant Root edits the Tenant's, the Console Operator an Event's in place of them while the Event
/// has not ended, and what an Event is printed with is its own logo for a position and the Tenant's where it has none. A logo is an
/// image of a kind a browser prints, of at most 512 KiB, sent as base64 and checked by what it starts with.
/// </summary>
public sealed class BrandingRoutesTests : IClassFixture<MongoFixture>
{
    const int LIMIT = 512 * 1024;
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

    readonly MongoFixture _mongo;

    public BrandingRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Tenant_Root_sets_a_logo_of_the_Tenant_reads_it_again_and_clears_it_and_what_it_does_not_name_stays()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var left = Png(100, 1);
        var right = Jpeg(200, 2);
        var nothing = await Edit(root, $"/api/tenants/{tenant}/branding", new { });
        Assert.Equal(HttpStatusCode.OK, nothing.StatusCode);
        Assert.Null(await FindAsync($"tenant:{tenant}")); // a document that names no logo makes none

        var first = await Edit(root, $"/api/tenants/{tenant}/branding", new { left = Document("image/png", left) });
        var second = await Edit(root, $"/api/tenants/{tenant}/branding", new { right = Document("image/jpeg", right) });
        var read = await root.Page.GetAsync($"/api/tenants/{tenant}/branding");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var attributes = (await Body(read)).GetProperty("data").GetProperty("attributes");
        Assert.Equal(("image/png", Convert.ToBase64String(left), 100), Shown(attributes.GetProperty("left")));
        Assert.Equal(("image/jpeg", Convert.ToBase64String(right), 200), Shown(attributes.GetProperty("right")));
        var stored = await StoredAsync($"tenant:{tenant}");
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal("image/png", stored["Left"]["ContentType"].AsString);
        Assert.Equal(left, stored["Left"]["Content"].AsBsonBinaryData.Bytes);

        var cleared = await Edit(root, $"/api/tenants/{tenant}/branding", new { left = (object?)null });

        var after = (await Body(cleared)).GetProperty("data").GetProperty("attributes");
        Assert.False(after.TryGetProperty("left", out _)); // a position that has none is not in the document
        Assert.Equal("image/jpeg", after.GetProperty("right").GetProperty("contentType").GetString());
        Assert.False((await StoredAsync($"tenant:{tenant}")).Contains("Left"));
    }

    [Fact]
    public async Task Nobody_but_a_Tenant_Root_of_the_Tenant_or_the_Developer_reads_or_edits_the_logos_of_the_Tenant()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var plain = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var ofOther = await SignedInAsync(api, client, _mongo.ConnectionString, other, TenantRootOf(other));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, other, isDeveloper: true);

        foreach (var person in new[] { plain, ofOther })
        {
            var write = await Edit(
                person,
                $"/api/tenants/{tenant}/branding",
                new { left = Document("image/png", Png(10, 1)) }
            );
            var read = await person.Page.GetAsync($"/api/tenants/{tenant}/branding");

            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
            Assert.Equal("not-root", await Code(write));
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        }

        Assert.Null(await FindAsync($"tenant:{tenant}"));
        var byDeveloper = await Edit(
            developer,
            $"/api/tenants/{tenant}/branding",
            new { left = Document("image/png", Png(10, 1)) }
        );
        Assert.Equal(HttpStatusCode.OK, byDeveloper.StatusCode);
        Assert.Equal(tenant, (await StoredAsync($"tenant:{tenant}"))["TenantId"].AsString); // in the Tenant it is of, and not the Developer's own
        var missing = await plain.Page.GetAsync("/api/tenants/country-nowhere/branding");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var visitor = ApiClients.Of(api);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await visitor.GetAsync($"/api/tenants/{tenant}/branding")).StatusCode
        );
    }

    [Fact]
    public async Task A_logo_that_is_not_one_is_refused_and_changes_nothing_not_even_the_other_logo_of_the_same_request()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var good = Document("image/png", Png(10, 1));
        var cases = new (object Attributes, HttpStatusCode Status, string Code)[]
        {
            (new { left = good, right = Document("image/png", Text()) }, HttpStatusCode.BadRequest, "invalid-logo"), // text that says it is an image
            (
                new { left = good, right = Document("image/jpeg", Png(10, 1)) },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ), // an image that is of another kind
            (new { left = good, right = Document("image/svg+xml", Text()) }, HttpStatusCode.BadRequest, "invalid-logo"), // a kind that is not printed
            (
                new { left = good, right = Document("image/png", NearMiss(0x89, 0x50, 0x4E, 0x47)) },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ), // the first bytes of a PNG and no more of its start
            (
                new { left = good, right = Document("image/jpeg", NearMiss(0xFF, 0xD8, 0x00)) },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ),
            (
                new { left = good, right = Document("image/gif", NearMiss(0x47, 0x49, 0x46, 0x00)) },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ),
            (
                new
                {
                    left = good,
                    right = Document(
                        "image/webp",
                        NearMiss(0x52, 0x49, 0x46, 0x46, 0x10, 0, 0, 0, 0x57, 0x41, 0x56, 0x45)
                    ),
                },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ), // a WAVE file is RIFF too
            (
                new
                {
                    left = good,
                    right = Document(
                        "image/webp",
                        NearMiss(0x52, 0x49, 0x46, 0x46, 0x10, 0, 0, 0, 0x57, 0x45, 0x42, 0x58)
                    ),
                },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ), // and one that says WEBX
            (
                new { left = good, right = Document("image/webp", [0x52, 0x49, 0x46, 0x46]) },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ), // RIFF and nothing after it
            (
                new { left = good, right = new { contentType = "image/png", content = "not base64!" } },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ),
            (
                new { left = good, right = new { contentType = "image/png", content = string.Empty } },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ),
            (new { left = good, right = new { contentType = "image/png" } }, HttpStatusCode.BadRequest, "invalid-logo"),
            (
                new
                {
                    left = good,
                    right = new
                    {
                        contentType = "image/png",
                        content = Convert.ToBase64String(Png(10, 1)), // an image that is good, with a member that is not a logo's
                        other = 1,
                    },
                },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ),
            (new { left = good, right = "a text" }, HttpStatusCode.BadRequest, "invalid-logo"),
            (new { left = good, right = new { content = "AA==" } }, HttpStatusCode.BadRequest, "invalid-logo"), // no kind
            (
                new { left = good, right = new { contentType = 5, content = "AA==" } },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ), // a kind that is not a text
            (
                new { left = good, right = new { contentType = "image/png", content = 5 } },
                HttpStatusCode.BadRequest,
                "invalid-logo"
            ), // nor is a content
            (
                new { left = good, right = new { contentType = "image/png", content = new string('!', 800_000) } },
                HttpStatusCode.UnprocessableEntity,
                "logo-too-large"
            ), // a text that is longer than an image of the limit can be is refused for its weight, before it is read
            (
                new { left = good, right = Document("image/png", Png(LIMIT + 1, 1)) },
                HttpStatusCode.UnprocessableEntity,
                "logo-too-large"
            ),
            (new { left = good, middle = good }, HttpStatusCode.BadRequest, "unsupported-attribute"),
            (
                new Dictionary<string, object> { ["left"] = good, ["LEFT"] = good },
                HttpStatusCode.BadRequest,
                "malformed-request"
            ), // a logo is named once, whatever its case
        };

        foreach (var (attributes, status, code) in cases)
        {
            var refused = await Edit(root, $"/api/tenants/{tenant}/branding", attributes);

            Assert.Equal(status, refused.StatusCode);
            Assert.Equal(code, await Code(refused));
        }

        Assert.Null(await FindAsync($"tenant:{tenant}")); // the good logo of each of them was not kept
        var atTheLimit = await Edit(
            root,
            $"/api/tenants/{tenant}/branding",
            new { left = Document("image/png", Png(LIMIT, 1)) }
        );
        Assert.Equal(HttpStatusCode.OK, atTheLimit.StatusCode);
    }

    [Fact]
    public async Task A_document_for_another_resource_than_the_route_names_or_for_one_that_is_not_there_is_not_taken()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, root.Id, "Not yet");
        var logo = new { left = Document("image/png", Png(10, 1)) };

        var anotherTenant = await root.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/tenants/{tenant}/branding",
            "brandings",
            logo,
            id: "country-xx"
        );
        var anotherEvent = await root.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/events/{unstarted}/branding",
            "brandings",
            logo,
            id: Guid.NewGuid().ToString()
        );

        Assert.Equal(HttpStatusCode.Conflict, anotherTenant.StatusCode);
        Assert.Equal("id-mismatch", await Code(anotherTenant));
        Assert.Equal(HttpStatusCode.Conflict, anotherEvent.StatusCode);
        Assert.Equal("id-mismatch", await Code(anotherEvent));
        Assert.Null(await FindAsync($"tenant:{tenant}"));
        Assert.Null(await FindAsync($"event:{unstarted}"));
        foreach (
            var missing in new[]
            {
                "/api/tenants/country-zz/branding",
                $"/api/events/{Guid.NewGuid()}/branding",
                "/api/events/not-an-id/branding",
            }
        )
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Edit(root, missing, logo)).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await root.Page.GetAsync("/api/tenants/country-zz/branding")).StatusCode
        );
    }

    [Fact]
    public async Task Every_kind_a_browser_prints_is_taken_by_what_it_starts_with()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        foreach (
            var (type, bytes) in new[]
            {
                ("image/png", Png(30, 1)),
                ("image/jpeg", Jpeg(30, 1)),
                ("image/gif", Gif(30)),
                ("image/webp", Webp(30)),
            }
        )
        {
            var taken = await Edit(root, $"/api/tenants/{tenant}/branding", new { left = Document(type, bytes) });

            Assert.Equal(HttpStatusCode.OK, taken.StatusCode);
            Assert.Equal(
                type,
                (await Body(taken))
                    .GetProperty("data")
                    .GetProperty("attributes")
                    .GetProperty("left")
                    .GetProperty("contentType")
                    .GetString()
            );
        }

        var upperCase = await Edit(
            root,
            $"/api/tenants/{tenant}/branding",
            new { right = Document("IMAGE/PNG", Png(30, 1)) }
        );
        Assert.Equal(HttpStatusCode.OK, upperCase.StatusCode);
        Assert.Equal("image/png", (await StoredAsync($"tenant:{tenant}"))["Right"]["ContentType"].AsString);

        var spaced = await Edit(
            root,
            $"/api/tenants/{tenant}/branding",
            new { right = Document("  image/jpeg ", Jpeg(30, 1)) }
        );
        Assert.Equal(HttpStatusCode.OK, spaced.StatusCode);
        Assert.Equal("image/jpeg", (await StoredAsync($"tenant:{tenant}"))["Right"]["ContentType"].AsString);
    }

    [Fact]
    public async Task A_logo_that_is_sent_back_as_it_was_read_is_taken_and_what_the_Api_computes_in_it_is_ignored()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var image = Png(40, 3);
        var read = new
        {
            contentType = "image/png",
            content = Convert.ToBase64String(image),
            size = 9999,
            source = "event",
        };

        var response = await Edit(root, $"/api/tenants/{tenant}/branding", new { left = read });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logo = (await Body(response)).GetProperty("data").GetProperty("attributes").GetProperty("left");
        Assert.Equal(40, logo.GetProperty("size").GetInt32()); // the weight of the image, whatever it was sent as
        Assert.False(logo.TryGetProperty("source", out _)); // and whose it is, which a Tenant's logo has no need to say
        Assert.Equal(image, (await StoredAsync($"tenant:{tenant}"))["Left"]["Content"].AsBsonBinaryData.Bytes);
    }

    [Fact]
    public async Task Two_large_logos_in_one_request_are_more_than_a_request_may_hold()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));

        var response = await Edit(
            root,
            $"/api/tenants/{tenant}/branding",
            new { left = Document("image/png", Png(LIMIT, 1)), right = Document("image/png", Png(LIMIT, 2)) }
        );

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("payload-too-large", await Code(response));
        Assert.Null(await FindAsync($"tenant:{tenant}"));
    }

    [Fact]
    public async Task The_Console_Operator_puts_a_logo_of_its_Event_in_place_of_the_Tenants_and_an_Event_is_printed_with_the_logo_it_has_or_the_Tenants()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );
        var tenantsLeft = Png(40, 1);
        var tenantsRight = Png(50, 2);
        var eventsLeft = Png(60, 3);
        await Edit(
            root,
            $"/api/tenants/{tenant}/branding",
            new { left = Document("image/png", tenantsLeft), right = Document("image/png", tenantsRight) }
        );

        var before = await official.Page.GetAsync($"/api/events/{eventId}/branding"); // any signed-in person reads what an Event is printed with
        var set = await Edit(
            consoleOperator,
            $"/api/events/{eventId}/branding",
            new { left = Document("image/png", eventsLeft) }
        );
        var cleared = await Edit(consoleOperator, $"/api/events/{eventId}/branding", new { left = (object?)null });

        var beforeBody = await Body(before);
        var setBody = await Body(set);
        var clearedBody = await Body(cleared);
        Assert.Equal(("tenant", 40), Source(beforeBody, "left"));
        Assert.Equal(("tenant", 50), Source(beforeBody, "right"));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(("event", 60), Source(setBody, "left")); // its own where it has one
        Assert.Equal(("tenant", 50), Source(setBody, "right")); // and the Tenant's where it has none
        Assert.Equal(("tenant", 40), Source(clearedBody, "left")); // clearing the override gives back the Tenant's
        Assert.Equal(eventId.ToString(), clearedBody.GetProperty("data").GetProperty("id").GetString());
        Assert.Equal(tenant, (await StoredAsync($"tenant:{tenant}"))["TenantId"].AsString);
        var kept = await StoredAsync($"event:{eventId}");
        Assert.Equal(tenant, kept["TenantId"].AsString);
        Assert.False(kept.Contains("Left"));
    }

    [Fact]
    public async Task Nobody_but_Root_and_a_Console_Operator_edits_the_logos_of_an_Event_and_not_once_it_has_ended()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var operatorGrant = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Not yet");
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Operator",
            null,
            operatorGrant.Email,
            operatorGrant.Id
        );
        var logo = new { left = Document("image/png", Png(10, 1)) };

        var refused = await Edit(operatorGrant, $"/api/events/{live}/branding", logo);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("not-console-operator", await Code(refused));

        var beforeItStarted = await Edit(consoleOperator, $"/api/events/{unstarted}/branding", logo); // an Event is branded as it is made ready
        var ended = await Edit(consoleOperator, $"/api/events/{historic}/branding", logo);
        var read = await operatorGrant.Page.GetAsync($"/api/events/{historic}/branding"); // what it was printed with is still read

        Assert.Equal(HttpStatusCode.OK, beforeItStarted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ended.StatusCode);
        Assert.Equal("event-ended", await Code(ended));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Null(await FindAsync($"event:{live}"));
        Assert.Null(await FindAsync($"event:{historic}"));
        var byRoot = await Edit(root, $"/api/events/{live}/branding", logo); // Root runs every Event of its Tenant
        Assert.Equal(HttpStatusCode.OK, byRoot.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await consoleOperator.Page.GetAsync($"/api/events/{Guid.NewGuid()}/branding")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await consoleOperator.Page.GetAsync("/api/events/not-an-id/branding")).StatusCode
        );
    }

    [Fact]
    public async Task The_logos_of_an_Event_that_was_deleted_before_it_started_go_with_it_and_those_of_another_stay()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var doomed = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, root.Id, "Doomed");
        var kept = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, root.Id, "Kept");
        var logo = new { left = Document("image/png", Png(10, 1)) };
        await Edit(root, $"/api/events/{doomed}/branding", logo);
        await Edit(root, $"/api/events/{kept}/branding", logo);

        var deleted = await root.Page.DeleteAsync($"/api/configure-events/{doomed}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Null(await FindAsync($"event:{doomed}"));
        Assert.NotNull(await FindAsync($"event:{kept}"));
    }

    static Task<HttpResponseMessage> Edit(Person person, string path, object attributes)
    {
        return person.Page.WriteAsync(HttpMethod.Patch, path, "brandings", attributes);
    }

    static object Document(string contentType, byte[] image)
    {
        return new { contentType, content = Convert.ToBase64String(image) };
    }

    static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        return await ApiSessions.ReadJsonAsync(response);
    }

    static async Task<string?> Code(HttpResponseMessage response)
    {
        return (await Body(response)).GetProperty("errors")[0].GetProperty("code").GetString();
    }

    static (string ContentType, string Content, int Size) Shown(JsonElement logo)
    {
        return (
            logo.GetProperty("contentType").GetString()!,
            logo.GetProperty("content").GetString()!,
            logo.GetProperty("size").GetInt32()
        );
    }

    static (string? Source, int Size) Source(JsonElement body, string position)
    {
        var logo = body.GetProperty("data").GetProperty("attributes").GetProperty(position);
        return (logo.GetProperty("source").GetString(), logo.GetProperty("size").GetInt32());
    }

    /// <summary>An image of the kind, of the size, that begins as such an image does and is filled with a byte of its own.</summary>
    static byte[] Png(int size, byte fill)
    {
        return Filled(size, fill, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    static byte[] Jpeg(int size, byte fill)
    {
        return Filled(size, fill, [0xFF, 0xD8, 0xFF, 0xE0]);
    }

    static byte[] Gif(int size)
    {
        return Filled(size, 7, [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);
    }

    static byte[] Webp(int size)
    {
        return Filled(size, 7, [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]);
    }

    /// <summary>A file of a few bytes that begins with those given: what a check of less than the whole start of an image would take.</summary>
    static byte[] NearMiss(params int[] start)
    {
        return Filled(40, 1, [.. start.Select(x => (byte)x)]);
    }

    static byte[] Text()
    {
        return System.Text.Encoding.UTF8.GetBytes("This is not an image, whatever it says it is.");
    }

    static byte[] Filled(int size, byte fill, byte[] start)
    {
        var bytes = new byte[size];
        Array.Fill(bytes, fill);
        start.CopyTo(bytes, 0);
        return bytes;
    }

    async Task<BsonDocument> StoredAsync(string key)
    {
        return await FindAsync(key) ?? throw new InvalidOperationException($"No branding '{key}' is stored.");
    }

    async Task<BsonDocument?> FindAsync(string key)
    {
        return await RegistrySeed
            .Collection(_mongo.ConnectionString, "brandings")
            .Find(new BsonDocument("_id", key))
            .FirstOrDefaultAsync();
    }
}
