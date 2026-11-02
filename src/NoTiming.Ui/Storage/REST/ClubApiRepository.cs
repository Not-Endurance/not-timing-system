using System.Text.Json;
using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>The clubs as the Api serves them (#603): JSON:API documents at <c>/api/clubs</c> (ADR-0008).</summary>
public class ClubApiRepository : JsonApiRepository<Club, ClubModel>
{
    public ClubApiRepository(JsonApiClient client)
        : base("clubs", client) { }

    /// <summary>The rows of every Tenant's registry whose name the query finds, a page at a time (ADR-0010, ADR-0012).</summary>
    public Task<(IReadOnlyList<JsonElement> Rows, bool HasNext)> SearchAllTenants(
        ListQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return PageOf($"{Collection}/all-tenants", query, cancellationToken);
    }
}
