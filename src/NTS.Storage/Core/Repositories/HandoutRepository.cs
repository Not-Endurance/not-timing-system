using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class HandoutRepository : RestApiRepository<Handout, HandoutModel>, ITransient
{
    public HandoutRepository(NHttpClient client)
        : base("handouts", client) { }
}
