using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class OfficialRepository : EventScopedApiRepository<Official, OfficialModel>, ITransient
{
    public OfficialRepository(NHttpClient client, IServiceProvider serviceProvider)
        : base("officials", client, serviceProvider) { }
}
