using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The Athletes as the Api serves them (#603): JSON:API documents at <c>/api/athletes</c> (ADR-0008). The account an
/// Athlete is linked to is kept by the Api and never leaves it, so it is neither read nor sent, and an edit of an Athlete
/// leaves the link as it is.
/// </summary>
public class AthleteApiRepository : JsonApiRepository<Athlete, AthleteModel>
{
    public AthleteApiRepository(JsonApiClient client)
        : base("athletes", client, "user") { }
}
