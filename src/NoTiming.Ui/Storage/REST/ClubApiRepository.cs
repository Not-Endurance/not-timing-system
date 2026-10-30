using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>The clubs as the Api serves them (#603): JSON:API documents at <c>/api/clubs</c> (ADR-0008).</summary>
public class ClubApiRepository : JsonApiRepository<Club, ClubModel>
{
    public ClubApiRepository(JsonApiClient client)
        : base("clubs", client) { }
}
