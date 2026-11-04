using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Storage.Core.Repositories;

public class HandoutApiRepository : ApiRepository<Handout, HandoutModel>
{
    public HandoutApiRepository(NHttpClient client)
        : base("handouts", client) { }
}
