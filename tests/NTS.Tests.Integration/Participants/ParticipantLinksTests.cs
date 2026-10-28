using System.Net;
using System.Text.Json;
using MongoDB.Bson;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The links of a person to the registries (#638, ADR-0010, the rest-api skill): <c>/api/athletes/{id}/link</c> links the caller
/// as that Athlete and <c>/api/horses/{id}/link</c> adds that Horse to the caller's Horses. The routes name no account, so there
/// is no way to reach the links of another person; the Participant has to be a row of the registry of any Tenant; the profile of
/// the caller tells what they are linked to, with the names.
/// </summary>
public sealed class ParticipantLinksTests : IClassFixture<MongoFixture>
{
    const string PROFILE = "/api/me/profile";
    const string PARTICIPANT_LINKS = "participant-links";

    readonly MongoFixture _mongo;

    public ParticipantLinksTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_person_links_themselves_as_an_Athlete_of_any_Tenant_and_their_profile_tells_it_with_the_name()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            await TenantAsync(_mongo.ConnectionString)
        );
        var athlete = await AthleteAsync(await TenantAsync(_mongo.ConnectionString), "Иван Петров", "Ivan Petrov");
        var path = $"/api/athletes/{athlete}/link";

        var before = await person.Page.GetAsync(path);
        var profileBefore = await person.Page.GetAsync(PROFILE);
        var linked = await PostAsync(person, path);
        var again = await PostAsync(person, path);
        var read = await person.Page.GetAsync(path);
        var profile = await person.Page.GetAsync(PROFILE);
        var removed = await person.Page.DeleteAsync(path);
        var gone = await person.Page.GetAsync(path);
        var profileAfter = await person.Page.GetAsync(PROFILE);

        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.Equal("not-found", await ErrorCodeAsync(before));
        var untouched = (await ReadAsync(profileBefore, HttpStatusCode.OK)).GetProperty("attributes");
        Assert.False(untouched.TryGetProperty("athlete", out _));
        Assert.Equal(0, untouched.GetProperty("horses").GetArrayLength());
        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        Assert.Equal(path, linked.Headers.Location?.ToString());
        Assert.Equal("application/vnd.api+json", linked.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode); // the same link again is the link that was made
        Assert.Null(again.Headers.Location);
        foreach (var response in new[] { linked, again, read })
        {
            var data = (await ReadJsonAsync(response)).GetProperty("data");
            Assert.Equal(PARTICIPANT_LINKS, data.GetProperty("type").GetString());
            Assert.Equal(athlete.ToString(), data.GetProperty("id").GetString());
            Assert.Equal(["kind", "displayName"], data.GetProperty("attributes").EnumerateObject().Select(x => x.Name));
            Assert.Equal("Athlete", data.GetProperty("attributes").GetProperty("kind").GetString());
            Assert.Equal("Ivan Petrov", data.GetProperty("attributes").GetProperty("displayName").GetString());
        }

        var named = (await ReadAsync(profile, HttpStatusCode.OK)).GetProperty("attributes").GetProperty("athlete");
        Assert.Equal(athlete.ToString(), named.GetProperty("id").GetString());
        Assert.Equal("Athlete", named.GetProperty("kind").GetString());
        Assert.Equal("Ivan Petrov", named.GetProperty("displayName").GetString());
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.False(
            (await ReadAsync(profileAfter, HttpStatusCode.OK))
                .GetProperty("attributes")
                .TryGetProperty("athlete", out _)
        );
    }

    [Fact]
    public async Task A_person_is_one_Athlete_at_a_time_and_linking_another_moves_the_link()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var first = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var second = await AthleteAsync(tenant, "Георги Иванов", "Georgi Ivanov");

        var one = await PostAsync(person, $"/api/athletes/{first}/link");
        var two = await PostAsync(person, $"/api/athletes/{second}/link");
        var oldOne = await person.Page.GetAsync($"/api/athletes/{first}/link");
        var newOne = await person.Page.GetAsync($"/api/athletes/{second}/link");
        var profile = await person.Page.GetAsync(PROFILE);
        var removingTheOldOne = await person.Page.DeleteAsync($"/api/athletes/{first}/link");

        Assert.Equal(HttpStatusCode.Created, one.StatusCode);
        Assert.Equal(HttpStatusCode.Created, two.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newOne.StatusCode);
        Assert.Equal(
            second.ToString(),
            (await ReadAsync(profile, HttpStatusCode.OK))
                .GetProperty("attributes")
                .GetProperty("athlete")
                .GetProperty("id")
                .GetString()
        );
        Assert.Equal(HttpStatusCode.NotFound, removingTheOldOne.StatusCode); // it is not the link the person has
    }

    [Fact]
    public async Task A_person_adds_and_removes_Horses_and_their_profile_lists_them_in_the_order_they_were_added()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var fire = await HorseAsync(tenant, "Огън", "Fire");
        var wind = await HorseAsync(await TenantAsync(_mongo.ConnectionString), "Вятър", "Wind");

        var addedWind = await PostAsync(person, $"/api/horses/{wind}/link");
        var addedFire = await PostAsync(person, $"/api/horses/{fire}/link");
        var addedFireAgain = await PostAsync(person, $"/api/horses/{fire}/link");
        var profile = await person.Page.GetAsync(PROFILE);
        var removedWind = await person.Page.DeleteAsync($"/api/horses/{wind}/link");
        var removedWindAgain = await person.Page.DeleteAsync($"/api/horses/{wind}/link");
        var readFire = await person.Page.GetAsync($"/api/horses/{fire}/link");
        var readWind = await person.Page.GetAsync($"/api/horses/{wind}/link");
        var profileAfter = await person.Page.GetAsync(PROFILE);

        Assert.Equal(HttpStatusCode.Created, addedWind.StatusCode);
        Assert.Equal($"/api/horses/{wind}/link", addedWind.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.Created, addedFire.StatusCode);
        Assert.Equal(HttpStatusCode.OK, addedFireAgain.StatusCode);
        var link = (await ReadJsonAsync(addedWind)).GetProperty("data");
        Assert.Equal(PARTICIPANT_LINKS, link.GetProperty("type").GetString());
        Assert.Equal(wind.ToString(), link.GetProperty("id").GetString());
        Assert.Equal("Horse", link.GetProperty("attributes").GetProperty("kind").GetString());
        Assert.Equal("Wind", link.GetProperty("attributes").GetProperty("displayName").GetString());
        var horses = (await ReadAsync(profile, HttpStatusCode.OK)).GetProperty("attributes").GetProperty("horses");
        Assert.Equal(
            [("Horse", wind.ToString(), "Wind"), ("Horse", fire.ToString(), "Fire")],
            horses
                .EnumerateArray()
                .Select(x =>
                    (
                        x.GetProperty("kind").GetString()!,
                        x.GetProperty("id").GetString()!,
                        x.GetProperty("displayName").GetString()!
                    )
                )
        );
        Assert.Equal(HttpStatusCode.NoContent, removedWind.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedWindAgain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readFire.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, readWind.StatusCode);
        Assert.Equal(
            fire.ToString(),
            Assert
                .Single(
                    (await ReadAsync(profileAfter, HttpStatusCode.OK))
                        .GetProperty("attributes")
                        .GetProperty("horses")
                        .EnumerateArray()
                )
                .GetProperty("id")
                .GetString()
        );
    }

    [Fact]
    public async Task A_person_and_an_Athlete_and_a_Horse_are_linked_independently()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var horse = await HorseAsync(tenant, "Огън", "Fire");

        await PostAsync(person, $"/api/athletes/{athlete}/link");
        await PostAsync(person, $"/api/horses/{horse}/link");
        var removedTheHorse = await person.Page.DeleteAsync($"/api/horses/{horse}/link");
        var profile = (await ReadAsync(await person.Page.GetAsync(PROFILE), HttpStatusCode.OK)).GetProperty(
            "attributes"
        );

        Assert.Equal(HttpStatusCode.NoContent, removedTheHorse.StatusCode);
        Assert.Equal(athlete.ToString(), profile.GetProperty("athlete").GetProperty("id").GetString()); // the Athlete stays
        Assert.Equal(0, profile.GetProperty("horses").GetArrayLength());
    }

    [Fact]
    public async Task The_names_are_told_in_the_language_of_the_request_by_the_link_and_the_profile_alike()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var horse = await HorseAsync(tenant, "Огън", "Fire");

        var linkedInBulgarian = await PostAsync(person, $"/api/athletes/{athlete}/link", language: "bg");
        await PostAsync(person, $"/api/horses/{horse}/link");
        var readInBulgarian = await GetAsync(person, $"/api/horses/{horse}/link", language: "bg");
        var profileInBulgarian = await GetAsync(person, PROFILE, language: "bg");
        var profileInEnglish = await GetAsync(person, PROFILE, language: "en");

        Assert.Equal("Иван Петров", await DisplayNameOfAsync(linkedInBulgarian));
        Assert.Equal("Огън", await DisplayNameOfAsync(readInBulgarian));
        Assert.Contains("Accept-Language", linkedInBulgarian.Headers.Vary);
        Assert.Contains("Accept-Language", profileInBulgarian.Headers.Vary);
        var inBulgarian = (await ReadAsync(profileInBulgarian, HttpStatusCode.OK)).GetProperty("attributes");
        Assert.Equal("Иван Петров", inBulgarian.GetProperty("athlete").GetProperty("displayName").GetString());
        Assert.Equal("Огън", inBulgarian.GetProperty("horses")[0].GetProperty("displayName").GetString());
        var inEnglish = (await ReadAsync(profileInEnglish, HttpStatusCode.OK)).GetProperty("attributes");
        Assert.Equal("Ivan Petrov", inEnglish.GetProperty("athlete").GetProperty("displayName").GetString());
        Assert.Equal("Fire", inEnglish.GetProperty("horses")[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task The_links_of_a_person_are_theirs_alone()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var one = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var other = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var horse = await HorseAsync(tenant, "Огън", "Fire");
        await PostAsync(one, $"/api/athletes/{athlete}/link");
        await PostAsync(one, $"/api/horses/{horse}/link");

        var readByTheOther = await other.Page.GetAsync($"/api/athletes/{athlete}/link");
        var horseByTheOther = await other.Page.GetAsync($"/api/horses/{horse}/link");
        var removedByTheOther = await other.Page.DeleteAsync($"/api/athletes/{athlete}/link");
        var horseRemovedByTheOther = await other.Page.DeleteAsync($"/api/horses/{horse}/link");
        var profileOfTheOther = (await ReadAsync(await other.Page.GetAsync(PROFILE), HttpStatusCode.OK)).GetProperty(
            "attributes"
        );
        var profileOfTheFirst = (await ReadAsync(await one.Page.GetAsync(PROFILE), HttpStatusCode.OK)).GetProperty(
            "attributes"
        );

        Assert.Equal(HttpStatusCode.NotFound, readByTheOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, horseByTheOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedByTheOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, horseRemovedByTheOther.StatusCode);
        Assert.False(profileOfTheOther.TryGetProperty("athlete", out _));
        Assert.Equal(0, profileOfTheOther.GetProperty("horses").GetArrayLength());
        Assert.Equal(athlete.ToString(), profileOfTheFirst.GetProperty("athlete").GetProperty("id").GetString());
        Assert.Equal(1, profileOfTheFirst.GetProperty("horses").GetArrayLength());

        // what is kept is one document for the account that made the links, and none for the one that only read its own
        var stored = RegistrySeed.Collection(_mongo.ConnectionString, "user_links");
        Assert.Equal(1, await stored.CountDocumentsAsync(new BsonDocument("_id", RegistrySeed.Binary(one.Id))));
        Assert.Equal(0, await stored.CountDocumentsAsync(new BsonDocument("_id", RegistrySeed.Binary(other.Id))));
    }

    [Fact]
    public async Task A_row_that_is_not_in_the_registry_of_that_kind_cannot_be_linked()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var horse = await HorseAsync(tenant, "Огън", "Fire");

        var noAthlete = await PostAsync(person, $"/api/athletes/{Guid.NewGuid()}/link");
        var aHorseAsAnAthlete = await PostAsync(person, $"/api/athletes/{horse}/link");
        var noHorse = await PostAsync(person, $"/api/horses/{Guid.NewGuid()}/link");
        var anAthleteAsAHorse = await PostAsync(person, $"/api/horses/{athlete}/link");
        var notAnId = await PostAsync(person, "/api/athletes/nobody/link");
        var notAnIdToRead = await person.Page.GetAsync("/api/horses/nobody/link");
        var notAnIdToRemove = await person.Page.DeleteAsync("/api/horses/nobody/link");
        var profile = (await ReadAsync(await person.Page.GetAsync(PROFILE), HttpStatusCode.OK)).GetProperty(
            "attributes"
        );

        foreach (
            var refused in new[]
            {
                noAthlete,
                aHorseAsAnAthlete,
                noHorse,
                anAthleteAsAHorse,
                notAnId,
                notAnIdToRead,
                notAnIdToRemove,
            }
        )
        {
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            Assert.Equal("not-found", await ErrorCodeAsync(refused));
        }

        Assert.False(profile.TryGetProperty("athlete", out _)); // nothing was kept
        Assert.Equal(0, profile.GetProperty("horses").GetArrayLength());
    }

    [Fact]
    public async Task A_person_adds_at_most_a_hundred_Horses_and_one_they_have_is_added_again_all_the_same()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var horses = new List<Guid>();
        for (var i = 0; i < 101; i++)
        {
            horses.Add(await HorseAsync(tenant, $"Кон {i}", $"Horse {i}"));
        }

        foreach (var horse in horses.Take(100))
        {
            Assert.Equal(HttpStatusCode.Created, (await PostAsync(person, $"/api/horses/{horse}/link")).StatusCode);
        }

        var oneTooMany = await PostAsync(person, $"/api/horses/{horses[100]}/link");
        var oneTheyHave = await PostAsync(person, $"/api/horses/{horses[7]}/link");
        var removed = await person.Page.DeleteAsync($"/api/horses/{horses[7]}/link");
        var inItsPlace = await PostAsync(person, $"/api/horses/{horses[100]}/link");
        var profile = (await ReadAsync(await person.Page.GetAsync(PROFILE), HttpStatusCode.OK)).GetProperty(
            "attributes"
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, oneTooMany.StatusCode);
        Assert.Equal("horse-limit-reached", await ErrorCodeAsync(oneTooMany));
        Assert.Equal(HttpStatusCode.OK, oneTheyHave.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, inItsPlace.StatusCode);
        Assert.Equal(100, profile.GetProperty("horses").GetArrayLength());
        Assert.Equal(horses[100].ToString(), profile.GetProperty("horses")[99].GetProperty("id").GetString());
        var listed = profile
            .GetProperty("horses")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetString())
            .ToList();
        Assert.DoesNotContain(horses[7].ToString(), listed); // the one that was taken away, and not another
        Assert.Contains(horses[0].ToString(), listed);
    }

    [Fact]
    public async Task A_link_to_a_row_that_was_deleted_since_is_not_shown_and_the_person_can_still_take_it_away()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var kept = await HorseAsync(tenant, "Огън", "Fire");
        var gone = await HorseAsync(tenant, "Вятър", "Wind");
        await PostAsync(person, $"/api/athletes/{athlete}/link");
        await PostAsync(person, $"/api/horses/{gone}/link");
        await PostAsync(person, $"/api/horses/{kept}/link");
        await RegistrySeed
            .Collection(_mongo.ConnectionString, "athletes")
            .DeleteOneAsync(new BsonDocument("_id", RegistrySeed.Binary(athlete)));
        await RegistrySeed
            .Collection(_mongo.ConnectionString, "horses")
            .DeleteOneAsync(new BsonDocument("_id", RegistrySeed.Binary(gone)));

        var profile = (await ReadAsync(await person.Page.GetAsync(PROFILE), HttpStatusCode.OK)).GetProperty(
            "attributes"
        );
        var readAthlete = await person.Page.GetAsync($"/api/athletes/{athlete}/link");
        var readHorse = await person.Page.GetAsync($"/api/horses/{gone}/link");
        var removedAthlete = await person.Page.DeleteAsync($"/api/athletes/{athlete}/link");
        var removedHorse = await person.Page.DeleteAsync($"/api/horses/{gone}/link");

        Assert.False(profile.TryGetProperty("athlete", out _));
        Assert.Equal(
            kept.ToString(),
            Assert.Single(profile.GetProperty("horses").EnumerateArray()).GetProperty("id").GetString()
        );
        Assert.Equal(HttpStatusCode.NotFound, readAthlete.StatusCode); // there is no such row to read the link of
        Assert.Equal(HttpStatusCode.NotFound, readHorse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removedAthlete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removedHorse.StatusCode);
    }

    [Fact]
    public async Task A_profile_that_is_changed_tells_the_links_and_does_not_take_them_as_members()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var other = await AthleteAsync(tenant, "Георги Иванов", "Georgi Ivanov");
        await PostAsync(person, $"/api/athletes/{athlete}/link");

        var changed = await person.Page.WriteAsync(
            HttpMethod.Patch,
            PROFILE,
            "profiles",
            new
            {
                club = "Another Club",
                athlete = new
                {
                    id = other,
                    kind = "Athlete",
                    displayName = "Georgi Ivanov",
                },
            },
            id: person.Id.ToString()
        );

        var attributes = (await ReadAsync(changed, HttpStatusCode.OK)).GetProperty("attributes");
        Assert.Equal("Another Club", attributes.GetProperty("club").GetString());
        Assert.Equal(athlete.ToString(), attributes.GetProperty("athlete").GetProperty("id").GetString()); // not the one the document names
    }

    [Fact]
    public async Task Nobody_who_is_not_signed_in_reads_makes_or_takes_away_a_link_and_a_write_needs_the_header_of_the_pages()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await AthleteAsync(tenant, "Иван Петров", "Ivan Petrov");
        var path = $"/api/athletes/{athlete}/link";
        var visitor = new PageClient(client);

        var read = await client.GetAsync(path);
        var made = await PostAsync(visitor, path);
        var removed = await visitor.DeleteAsync(path);
        person.Page.WriteHeader = null;
        var withoutTheHeader = await PostAsync(person, path);
        var removedWithoutTheHeader = await person.Page.DeleteAsync(path);
        person.Page.WriteHeader = "NoTiming";

        foreach (var refused in new[] { read, made, removed })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.Equal("not-signed-in", await ErrorCodeAsync(refused));
        }

        Assert.Equal(HttpStatusCode.BadRequest, withoutTheHeader.StatusCode);
        Assert.Equal("request-header-required", await ErrorCodeAsync(withoutTheHeader));
        Assert.Equal("request-header-required", await ErrorCodeAsync(removedWithoutTheHeader));
        Assert.Equal(HttpStatusCode.NotFound, (await person.Page.GetAsync(path)).StatusCode); // nothing was made
    }

    Task<Guid> AthleteAsync(string tenant, string name, string nameEnglish)
    {
        return RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            name,
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: nameEnglish
        );
    }

    Task<Guid> HorseAsync(string tenant, string name, string nameEnglish)
    {
        return RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, name, nameEnglish);
    }

    static Task<HttpResponseMessage> PostAsync(Person person, string path, string? language = null)
    {
        return PostAsync(person.Page, path, language);
    }

    static Task<HttpResponseMessage> PostAsync(PageClient page, string path, string? language = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (language != null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }

        return page.SendAsync(request);
    }

    static Task<HttpResponseMessage> GetAsync(Person person, string path, string language)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.AcceptLanguage.ParseAdd(language);
        return person.Page.SendAsync(request);
    }

    static async Task<string?> DisplayNameOfAsync(HttpResponseMessage response)
    {
        return (await ReadJsonAsync(response))
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("displayName")
            .GetString();
    }

    static Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return ApiSessions.ReadJsonAsync(response);
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("data");
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
