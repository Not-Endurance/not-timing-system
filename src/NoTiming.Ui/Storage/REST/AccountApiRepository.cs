using System.Net;
using System.Text.Json;
using Not.Application.HTTP;
using Not.Blazor.Client.REST;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The account of the person who is signed in, as the Api serves it (ADR-0008, #645): <c>me</c>, its profile, its session, its
/// links to the Athletes and Horses they are, and the search of the accounts a Console Operator may assign. Every answer is
/// the document the Api gave; the services that keep the account read it as they need.
/// </summary>
public class AccountApiRepository
{
    readonly JsonApiClient _client;

    public AccountApiRepository(JsonApiClient client)
    {
        _client = client;
    }

    /// <summary>The time the Api says it is, which every answer carries, a refusal too; none when the Api has not answered.</summary>
    public async Task<DateTimeOffset?> ServerTime()
    {
        return (await JsonApiAnswers.Reached(_client.Get("me"))).ServerTime;
    }

    /// <summary>The account as the Api answers it; none when nobody is signed in (the Api answers 401).</summary>
    public async Task<JsonElement?> Me()
    {
        var response = await JsonApiAnswers.Reached(_client.Get("me"));
        if (response.Status == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        return ResourceOf(
            response.IsSuccess ? response : throw new JsonApiException(response, _client.UnavailableCodes)
        );
    }

    /// <summary>Ends the session the browser holds.</summary>
    public async Task SignOut()
    {
        await Taken(_client.Delete("sessions/current"));
    }

    /// <summary>Selects the Tenant the account acts in, or none.</summary>
    public async Task<JsonElement> SelectTenant(Guid accountId, string? tenantId)
    {
        return ResourceOf(
            await Taken(_client.Patch("me", "accounts", accountId.ToString(), new { selectedTenantId = tenantId }))
        );
    }

    /// <summary>The profile of the person who is signed in, as the Api answers it.</summary>
    public async Task<JsonElement> Profile()
    {
        return ResourceOf(await Taken(_client.Get("me/profile")));
    }

    /// <summary>Saves the profile: every member is named, so that one the person emptied is taken away.</summary>
    public async Task<JsonElement> SaveProfile(Guid accountId, object profile)
    {
        return ResourceOf(await Taken(_client.Patch("me/profile", "profiles", accountId.ToString(), profile)));
    }

    public async Task LinkAthlete(Guid athleteId)
    {
        await Taken(_client.Post($"athletes/{athleteId}/link"));
    }

    /// <summary>Takes the link away. A link that is gone already is taken away all the same.</summary>
    public async Task UnlinkAthlete(Guid athleteId)
    {
        await Unlink($"athletes/{athleteId}/link");
    }

    public async Task LinkHorse(Guid horseId)
    {
        await Taken(_client.Post($"horses/{horseId}/link"));
    }

    /// <summary>Takes the link away. A link that is gone already is taken away all the same.</summary>
    public async Task UnlinkHorse(Guid horseId)
    {
        await Unlink($"horses/{horseId}/link");
    }

    /// <summary>The accounts the filter finds, the first page of them; the rows are the Api's, as it lists them.</summary>
    public async Task<IReadOnlyList<JsonElement>> Search(string filter)
    {
        var response = await Taken(_client.Get("accounts", new ListQuery(filter: filter)));
        return [.. response.Document!.Value.GetProperty("data").EnumerateArray()];
    }

    async Task Unlink(string path)
    {
        var response = await JsonApiAnswers.Reached(_client.Delete(path));
        if (!response.IsSuccess && !response.IsNotFound)
        {
            throw new JsonApiException(response, _client.UnavailableCodes);
        }
    }

    async Task<JsonApiResponse> Taken(Task<JsonApiResponse> request)
    {
        return await JsonApiAnswers.Taken(request, _client.UnavailableCodes);
    }

    static JsonElement ResourceOf(JsonApiResponse response)
    {
        return response.Document!.Value.GetProperty("data");
    }
}
