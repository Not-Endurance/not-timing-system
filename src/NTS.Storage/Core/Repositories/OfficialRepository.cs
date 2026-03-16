using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class OfficialRepository : EventScopedApiRepository<Official, OfficialModel>, ITransient
{
    public OfficialRepository(NHttpClient client, INtsSocketContext socketContext)
        : base("officials", client, socketContext) { }
}
