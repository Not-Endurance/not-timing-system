using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Storage.Core.Repositories;

public class OfficialApiRepository : ApiRepository<Official, OfficialModel>
{
    public OfficialApiRepository(NHttpClient client)
        : base("officials", client) { }
}
