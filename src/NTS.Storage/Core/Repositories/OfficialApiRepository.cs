using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class OfficialApiRepository : ApiRepository<Official, OfficialModel>
{
    public OfficialApiRepository(NHttpClient client)
        : base("officials", client) { }
}
