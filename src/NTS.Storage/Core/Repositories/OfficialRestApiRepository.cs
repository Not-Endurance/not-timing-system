using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class OfficialRestApiRepository : ApiRepository<Official, OfficialModel>, ITransient
{
    public OfficialRestApiRepository(NHttpClient client)
        : base("officials", client) { }
}
