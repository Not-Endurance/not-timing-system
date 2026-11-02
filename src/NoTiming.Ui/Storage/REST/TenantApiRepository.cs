using System.Text.Json;
using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Tenancy.Models;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The Tenants as the Api serves them (ADR-0002, ADR-0012), at <c>/api/tenants/{id}</c>. A Tenant is keyed by text, such as
/// <c>country-bg</c>, so it is read and changed at its path rather than through the repositories that keep Guids.
/// </summary>
public class TenantApiRepository
{
    readonly JsonApiClient _client;

    public TenantApiRepository(JsonApiClient client)
    {
        _client = client;
    }

    /// <summary>The Tenant as the Api names it, or none when the Api has no such Tenant.</summary>
    public async Task<TenantModel?> Read(string tenantId)
    {
        var response = await JsonApiAnswers.Reached(_client.Get(Route(tenantId)));
        if (response.IsNotFound)
        {
            return null;
        }

        if (!response.IsSuccess)
        {
            throw new JsonApiException(response, _client.UnavailableCodes);
        }

        var resource = response.Document!.Value.GetProperty("data");
        var model = JsonSerializer.Deserialize<TenantModel>(
            resource.GetProperty("attributes").GetRawText(),
            _client.Json
        )!;
        model.Key = resource.GetProperty("id").GetString() ?? tenantId;
        return model;
    }

    /// <summary>What the person may do about the Tenant, as the Api says for them.</summary>
    public async Task<TenantCapabilitiesModel> CapabilitiesOf(string tenantId)
    {
        var response = await Taken(_client.Get($"{Route(tenantId)}/capabilities"));
        return JsonSerializer.Deserialize<TenantCapabilitiesModel>(
            ResourceOf(response).GetProperty("attributes").GetRawText(),
            _client.Json
        )!;
    }

    /// <summary>Saves the rules of the Tenant's Regional competitions. A ranker that is none is cleared.</summary>
    public async Task SaveRules(string tenantId, RegionalRulesModel rules)
    {
        await Taken(_client.Patch(Route(tenantId), "tenants", tenantId, new { regionalRules = rules }));
    }

    async Task<JsonApiResponse> Taken(Task<JsonApiResponse> request)
    {
        return await JsonApiAnswers.Taken(request, _client.UnavailableCodes);
    }

    static JsonElement ResourceOf(JsonApiResponse response)
    {
        return response.Document!.Value.GetProperty("data");
    }

    static string Route(string tenantId)
    {
        return $"tenants/{Uri.EscapeDataString(tenantId)}";
    }
}
