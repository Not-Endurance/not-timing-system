using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>
/// The Officials of an Event as the Api serves them (#604): the <c>officials</c> resource, by name and role. The account an
/// Official is linked to is the Api's, never shown and never sent, so what a visitor sees does not say who is behind a name.
/// </summary>
public class OfficialApiRepository : EventDataApiRepository<Official, OfficialModel>
{
    protected OfficialApiRepository(JsonApiClient client, IRepositoryScopeFactory<Official> scopeFactory)
        : base("officials", client, scopeFactory, "userId") { }

    public OfficialApiRepository(JsonApiClient client)
        : base("officials", client, null, "userId") { }
}
