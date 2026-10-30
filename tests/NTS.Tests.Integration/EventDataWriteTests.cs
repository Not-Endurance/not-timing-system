using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Features.Live;
using NTS.Contracts.Core.Models;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// Writing the rest of what an Event keeps (#604, ADR-0012, ADR-0013): the Rankings, Officials and Handouts it made when it
/// started, which the Main Operator of a Live Event makes, changes and removes as it does the Participations (see
/// <c>ParticipationWriteTests</c>), but which count no writes and are not announced: a change of them is the last one made,
/// and what the viewers follow is the Participations. The documents are seeded straight into MongoDB and read back from it.
/// </summary>
public sealed class EventDataWriteTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public EventDataWriteTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Main_Operator_makes_changes_and_removes_a_Ranking_that_nobody_is_told_of_and_that_counts_no_version()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var first = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 2, Guid.NewGuid());
        var ranking = IntegrationPayloadFactory.Ranking(eventId, [first, second], Guid.NewGuid(), "CEI 1*");

        var made = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/rankings",
            "rankings",
            JsonApiAttributes.Of(RankingModel.From(ranking)),
            id: ranking.Id.ToString()
        );
        var changed = await mainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/rankings/{ranking.Id}",
            "rankings",
            new { name = "CEI 2*" }
        );
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_rankings", ranking.Id))!;
        var removed = await mainOperator.Page.DeleteAsync($"/api/rankings/{ranking.Id}");

        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.Equal($"/api/rankings/{ranking.Id}", made.Headers.Location?.OriginalString);
        var data = (await ApiSessions.ReadJsonAsync(made)).GetProperty("data");
        Assert.False(data.TryGetProperty("meta", out _));
        Assert.Equal("CEI 1*", data.GetProperty("attributes").GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(
            "CEI 2*",
            (await ApiSessions.ReadJsonAsync(changed))
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("name")
                .GetString()
        );
        Assert.Equal("CEI 2*", stored["Name"].AsString);
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Equal(eventId, stored["EventId"].AsGuid);
        Assert.Equal(2, stored["Entries"].AsBsonArray.Count);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_rankings", ranking.Id));
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task A_row_stays_in_its_Event_and_in_its_Tenant_whatever_a_change_says()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        var handout = IntegrationPayloadFactory.Handout(participation, Guid.NewGuid());
        await EventSeed.HandoutAsync(_mongo.ConnectionString, tenant, handout);

        var elsewhere = await mainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/handouts/{handout.Id}",
            "handouts",
            new { eventId = Guid.NewGuid() }
        );
        var theirs = await mainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/handouts/{handout.Id}",
            "handouts",
            new { tenantId = "country-xx" }
        );
        var another = Guid.NewGuid();
        var moved = await mainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/handouts/{handout.Id}",
            "handouts",
            new { participationId = another }
        );

        Assert.Equal(HttpStatusCode.BadRequest, elsewhere.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(elsewhere));
        Assert.Equal(HttpStatusCode.BadRequest, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode); // what a Handout is a Handout of is what it names
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_handouts", handout.Id))!;
        Assert.Equal(another, stored["ParticipationId"].AsGuid);
        Assert.Equal(eventId, stored["EventId"].AsGuid);
        Assert.Equal(tenant, stored["TenantId"].AsString);
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task An_Official_is_made_and_changed_by_name_and_role_and_the_account_it_is_linked_to_is_not_the_documents_to_name()
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var linked = IntegrationPayloadFactory.Official(eventId, TestId.Of(77), Guid.NewGuid());
        await EventSeed.OfficialAsync(_mongo.ConnectionString, tenant, linked);
        var made = IntegrationPayloadFactory.Official(eventId, null, Guid.NewGuid());

        var naming = JsonApiAttributes.Of(OfficialModel.MapFrom(made), "userId");
        naming["userId"] = JsonSerializer.SerializeToElement(TestId.Of(78));
        var refused = await mainOperator.Page.WriteAsync(HttpMethod.Post, "/api/officials", "officials", naming);
        var created = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/officials",
            "officials",
            JsonApiAttributes.Of(OfficialModel.MapFrom(made), "userId"),
            id: made.Id.ToString()
        );
        var changed = await mainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/officials/{linked.Id}",
            "officials",
            new { role = "Steward" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("unsupported-attribute", await ErrorCodeAsync(refused));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var shown = (await ApiSessions.ReadJsonAsync(created)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("Integration Official", shown.GetProperty("name").GetString());
        Assert.False(shown.TryGetProperty("userId", out _));
        Assert.False(
            (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_officials", made.Id))!.Contains("UserId")
        );
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_officials", linked.Id))!;
        Assert.Equal("Steward", stored["Role"].AsString);
        Assert.Equal(TestId.Of(77), stored["UserId"].AsGuid); // a change of the Official leaves the account it is linked to
        Assert.Empty(changes.Announced);
    }

    [Theory]
    [InlineData("rankings")]
    [InlineData("officials")]
    [InlineData("handouts")]
    public async Task Every_family_is_written_by_the_Main_Operator_of_a_Live_Event_and_by_nobody_else(string route)
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        using var anonymous = ApiClients.Of(api);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var existing = EventDataRow.Of(route, live);
        var ended = EventDataRow.Of(route, historic);
        await existing.SeedAsync(_mongo.ConnectionString, tenant);
        await ended.SeedAsync(_mongo.ConnectionString, tenant);

        foreach (var person in new[] { member, developer })
        {
            var made = await person.Page.WriteAsync(
                HttpMethod.Post,
                $"/api/{route}",
                route,
                EventDataRow.Of(route, live).Attributes
            );
            var changed = await person.Page.WriteAsync(HttpMethod.Patch, $"/api/{route}/{existing.Id}", route, new { });
            var removed = await person.Page.DeleteAsync($"/api/{route}/{existing.Id}");

            foreach (var refused in new[] { made, changed, removed })
            {
                Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
                Assert.Equal("not-main-operator", await ErrorCodeAsync(refused));
            }
        }

        var page = new PageClient(anonymous);
        var signedOut = new[]
        {
            await page.WriteAsync(HttpMethod.Post, $"/api/{route}", route, existing.Attributes),
            await page.WriteAsync(HttpMethod.Patch, $"/api/{route}/{existing.Id}", route, new { }),
            await page.DeleteAsync($"/api/{route}/{existing.Id}"),
        };
        var early = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/{route}",
            route,
            EventDataRow.Of(route, unstarted).Attributes
        );
        var late = new[]
        {
            await mainOperator.Page.WriteAsync(
                HttpMethod.Post,
                $"/api/{route}",
                route,
                EventDataRow.Of(route, historic).Attributes
            ),
            await mainOperator.Page.WriteAsync(HttpMethod.Patch, $"/api/{route}/{ended.Id}", route, new { }),
            await mainOperator.Page.DeleteAsync($"/api/{route}/{ended.Id}"),
        };

        Assert.All(signedOut, x => Assert.Equal(HttpStatusCode.Unauthorized, x.StatusCode));
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("event-not-started", await ErrorCodeAsync(early));
        foreach (var refused in late)
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("event-ended", await ErrorCodeAsync(refused));
        }

        Assert.Equal(2, await RegistrySeed.CountAsync(_mongo.ConnectionString, existing.Collection, tenant));
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, existing.Collection, existing.Id));
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, existing.Collection, ended.Id));
        Assert.Empty(changes.Announced);
    }

    [Theory]
    [InlineData("rankings")]
    [InlineData("officials")]
    [InlineData("handouts")]
    public async Task A_row_is_not_changed_removed_or_made_through_the_Tenant_of_another_Event(string route)
    {
        var changes = new RecordedChanges();
        await using var api = NewApi(changes);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var otherTenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        var foreign = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            otherTenant,
            TestId.Of(99),
            DateTimeOffset.UtcNow
        );
        var theirs = EventDataRow.Of(route, foreign);
        await theirs.SeedAsync(_mongo.ConnectionString, otherTenant);

        var made = await mainOperator.Page.WriteAsync(HttpMethod.Post, $"/api/{route}", route, theirs.Attributes);
        var changed = await mainOperator.Page.WriteAsync(HttpMethod.Patch, $"/api/{route}/{theirs.Id}", route, new { });
        var removed = await mainOperator.Page.DeleteAsync($"/api/{route}/{theirs.Id}");
        var taken = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/{route}",
            route,
            EventDataRow.Of(route, live).Attributes,
            id: theirs.Id.ToString()
        );

        foreach (var refused in new[] { made, changed, removed })
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode); // the Main Operator of one Event is nobody to another
            Assert.Equal("not-main-operator", await ErrorCodeAsync(refused));
        }

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("id-taken", await ErrorCodeAsync(taken));
        Assert.Equal(1, await RegistrySeed.CountAsync(_mongo.ConnectionString, theirs.Collection, otherTenant));
        Assert.Equal(0, await RegistrySeed.CountAsync(_mongo.ConnectionString, theirs.Collection, tenant));
        Assert.Empty(changes.Announced);
    }

    ApiFactory NewApi(IParticipationChanges changes)
    {
        return new ApiFactory(_mongo.ConnectionString, configureServices: services => services.AddSingleton(changes));
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
