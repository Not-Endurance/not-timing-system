using System.Text.Json;
using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>The horses as the Api serves them (#603): JSON:API documents at <c>/api/horses</c> (ADR-0008).</summary>
public class HorseApiRepository : JsonApiRepository<Horse, HorseModel>
{
    public HorseApiRepository(JsonApiClient client)
        : base("horses", client) { }

    /// <summary>The rows of every Tenant's registry whose name the query finds, a page at a time (ADR-0010, ADR-0012).</summary>
    public Task<(IReadOnlyList<JsonElement> Rows, bool HasNext)> SearchAllTenants(
        ListQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return PageOf($"{Collection}/all-tenants", query, cancellationToken);
    }
}
