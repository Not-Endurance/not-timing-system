using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// An Athlete or a Horse of the registry that a Participation refers to cannot be deleted (#635, ADR-0010): the copies a
/// Participation keeps carry the id of their row, so a row that is deleted would leave them with nothing they come from. The
/// Participations that count are those of the Events that have started, in any Tenant, and those of an Event that has not, and
/// the refusal is the Api's, so that every client is held to it. A row that nothing refers to is deleted as before. Over the real
/// Api in this process.
/// </summary>
public sealed class RegistryRowInUseTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public RegistryRowInUseTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_Athlete_and_a_Horse_that_a_Participation_of_a_started_Event_names_are_refused_and_stay_until_nothing_names_them()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var participation = await RiddenAsync(tenant);
        var (athlete, horse) = await RowsAsync(tenant, participation);

        var athleteRefused = await root.Page.DeleteAsync($"/api/athletes/{athlete}");
        var horseRefused = await root.Page.DeleteAsync($"/api/horses/{horse}");
        await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_participations")
            .DeleteOneAsync(new BsonDocument("_id", RegistrySeed.Binary(participation.Id)));
        var athleteDeleted = await root.Page.DeleteAsync($"/api/athletes/{athlete}");
        var horseDeleted = await root.Page.DeleteAsync($"/api/horses/{horse}");

        Assert.Equal(HttpStatusCode.Conflict, athleteRefused.StatusCode);
        Assert.Equal("athlete-in-use", await ErrorCodeAsync(athleteRefused));
        Assert.Equal(HttpStatusCode.Conflict, horseRefused.StatusCode);
        Assert.Equal("horse-in-use", await ErrorCodeAsync(horseRefused));
        Assert.Equal(HttpStatusCode.NoContent, athleteDeleted.StatusCode); // nothing names them any more
        Assert.Equal(HttpStatusCode.NoContent, horseDeleted.StatusCode);
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", athlete));
        Assert.Null(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "horses", horse));
    }

    [Fact]
    public async Task A_row_stays_while_a_refused_delete_is_asked_again_and_says_the_same()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var participation = await RiddenAsync(tenant);
        var (athlete, _) = await RowsAsync(tenant, participation);

        var first = await root.Page.DeleteAsync($"/api/athletes/{athlete}");
        var second = await root.Page.DeleteAsync($"/api/athletes/{athlete}");

        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", athlete));
    }

    [Fact]
    public async Task A_row_that_a_Participation_of_another_Tenants_Event_names_is_refused_too()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var participation = await RiddenAsync(other); // an Event of another Tenant rides the Athlete of this one
        var (athlete, horse) = await RowsAsync(tenant, participation);

        var athleteRefused = await root.Page.DeleteAsync($"/api/athletes/{athlete}");
        var horseRefused = await root.Page.DeleteAsync($"/api/horses/{horse}");

        Assert.Equal(HttpStatusCode.Conflict, athleteRefused.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, horseRefused.StatusCode);
    }

    [Fact]
    public async Task A_row_that_a_Participation_of_an_Event_that_has_not_started_names_is_refused_too()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var setupId = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, root.Id);
        var setup = (await EventSeed.SetupOfAsync(_mongo.ConnectionString, setupId))!;
        var combination = setup["Competitions"][0]["Participations"][0]["Combination"].AsBsonDocument;
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Мариела Тестова",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            id: combination["Athlete"]["_id"].AsGuid
        );
        var horse = await RegistrySeed.HorseAsync(
            _mongo.ConnectionString,
            tenant,
            "Тест Огън",
            id: combination["Horse"]["_id"].AsGuid
        );

        var athleteRefused = await root.Page.DeleteAsync($"/api/athletes/{athlete}");
        var horseRefused = await root.Page.DeleteAsync($"/api/horses/{horse}");

        Assert.Equal("athlete-in-use", await ErrorCodeAsync(athleteRefused));
        Assert.Equal("horse-in-use", await ErrorCodeAsync(horseRefused));
    }

    [Fact]
    public async Task A_row_that_nothing_names_is_deleted_as_before_and_a_Club_is_not_held_to_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Борис Примеров",
            RegistrySeed.CountryOf("Bulgaria", "BG")
        );
        var horse = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Друг Кон");
        var club = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Sofia Riders");
        await RiddenAsync(tenant); // a Participation that names other rows

        var athleteDeleted = await root.Page.DeleteAsync($"/api/athletes/{athlete}");
        var horseDeleted = await root.Page.DeleteAsync($"/api/horses/{horse}");
        var clubDeleted = await root.Page.DeleteAsync($"/api/clubs/{club}");
        var again = await root.Page.DeleteAsync($"/api/athletes/{athlete}");

        Assert.Equal(HttpStatusCode.NoContent, athleteDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, horseDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, clubDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task A_row_of_another_Tenant_is_not_found_whether_a_Participation_names_it_or_not()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var participation = await RiddenAsync(other);
        var (named, _) = await RowsAsync(other, participation);
        var unnamed = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            other,
            "Ясен Проверов",
            RegistrySeed.CountryOf("Bulgaria", "BG")
        );

        var ofNamed = await root.Page.DeleteAsync($"/api/athletes/{named}");
        var ofUnnamed = await root.Page.DeleteAsync($"/api/athletes/{unnamed}");

        Assert.Equal(HttpStatusCode.NotFound, ofNamed.StatusCode); // nothing tells that it is in use
        Assert.Equal(HttpStatusCode.NotFound, ofUnnamed.StatusCode);
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", named));
        Assert.NotNull(await RegistrySeed.StoredAsync(_mongo.ConnectionString, "athletes", unnamed));
    }

    [Fact]
    public async Task A_person_who_may_not_edit_the_registry_is_refused_before_the_row_is_asked_about()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var participation = await RiddenAsync(tenant);
        var (athlete, _) = await RowsAsync(tenant, participation);

        var refused = await member.Page.DeleteAsync($"/api/athletes/{athlete}");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.NotEqual("athlete-in-use", await ErrorCodeAsync(refused));
    }

    /// <summary>A Participation of a Live Event of the Tenant, which names an Athlete and a Horse by their ids.</summary>
    async Task<NTS.Domain.Core.Aggregates.Participation> RiddenAsync(string tenant)
    {
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, DateTimeOffset.UtcNow);
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        return participation;
    }

    /// <summary>The rows of the Tenant's registry that the copies of the Participation come from.</summary>
    async Task<(Guid Athlete, Guid Horse)> RowsAsync(string tenant, NTS.Domain.Core.Aggregates.Participation named)
    {
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Мариела Тестова",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            id: named.Combination.Athlete.Id
        );
        var horse = await RegistrySeed.HorseAsync(
            _mongo.ConnectionString,
            tenant,
            "Тест Огън",
            id: named.Combination.Horse.Id
        );
        return (athlete, horse);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
