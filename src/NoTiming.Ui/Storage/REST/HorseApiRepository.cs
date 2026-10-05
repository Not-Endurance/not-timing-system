using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>The horses as the Api serves them (#603): JSON:API documents at <c>/api/horses</c> (ADR-0008).</summary>
public class HorseApiRepository : JsonApiRepository<Horse, HorseModel>
{
    public HorseApiRepository(JsonApiClient client)
        : base("horses", client) { }
}
