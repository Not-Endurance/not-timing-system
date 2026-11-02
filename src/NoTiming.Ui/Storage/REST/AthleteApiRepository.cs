using System.Text.Json;
using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The Athletes as the Api serves them (#603): JSON:API documents at <c>/api/athletes</c> (ADR-0008). The account an
/// Athlete is linked to is kept by the Api and never leaves it: the model holds no such member, so it is neither read nor
/// sent, and an edit of an Athlete leaves the link as it is.
/// </summary>
public class AthleteApiRepository : JsonApiRepository<Athlete, AthleteModel>
{
    public AthleteApiRepository(JsonApiClient client)
        : base("athletes", client) { }

    /// <summary>The rows of every Tenant's registry whose name the query finds, a page at a time (ADR-0010, ADR-0012).</summary>
    public Task<(IReadOnlyList<JsonElement> Rows, bool HasNext)> SearchAllTenants(
        ListQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return PageOf($"{Collection}/all-tenants", query, cancellationToken);
    }
}
