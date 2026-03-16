using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class HandoutRepository : EventScopedApiRepository<Handout, HandoutModel>, ITransient
{
    public HandoutRepository(NHttpClient client, IServiceProvider serviceProvider)
        : base("handouts", client, serviceProvider) { }
}
