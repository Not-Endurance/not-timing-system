using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class OfficialRepository : RestApiRepository<Official, OfficialModel>, ITransient
{
    public OfficialRepository(NHttpClient client)
        : base("officials", client) { }
}
