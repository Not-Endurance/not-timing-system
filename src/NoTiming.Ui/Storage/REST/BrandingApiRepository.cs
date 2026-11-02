using System.Text.Json;
using System.Text.Json.Nodes;
using Not.Application.HTTP;
using Not.Blazor.Client.REST;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The logos of a Tenant and of an Event as the Api serves them (ADR-0012), at <c>tenants/{id}/branding</c> and
/// <c>events/{id}/branding</c>. A change names only the logo it changes: a logo that is not named stays as it is.
/// </summary>
public class BrandingApiRepository
{
    const string BRANDINGS = "brandings";

    readonly JsonApiClient _client;

    public BrandingApiRepository(JsonApiClient client)
    {
        _client = client;
    }

    /// <summary>The logos of the Tenant, as the Api answers them.</summary>
    public async Task<JsonElement> OfTenant(string tenantId)
    {
        return await Read($"tenants/{Uri.EscapeDataString(tenantId)}/branding");
    }

    /// <summary>The logos of the Event, as the Api answers them.</summary>
    public async Task<JsonElement> OfEvent(Guid eventId)
    {
        return await Read($"events/{eventId}/branding");
    }

    public async Task ChangeOfTenant(string tenantId, IReadOnlyDictionary<string, JsonNode?> logo)
    {
        await Change($"tenants/{Uri.EscapeDataString(tenantId)}/branding", logo);
    }

    public async Task ChangeOfEvent(Guid eventId, IReadOnlyDictionary<string, JsonNode?> logo)
    {
        await Change($"events/{eventId}/branding", logo);
    }

    async Task<JsonElement> Read(string path)
    {
        var response = await JsonApiAnswers.Taken(_client.Get(path), _client.UnavailableCodes);
        return response.Document!.Value.GetProperty("data");
    }

    async Task Change(string path, IReadOnlyDictionary<string, JsonNode?> logo)
    {
        await JsonApiAnswers.Taken(_client.Patch(path, BRANDINGS, null, logo), _client.UnavailableCodes);
    }
}
