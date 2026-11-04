using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Setup;
using NoTiming.Application.Contracts.Setup.Models;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Storage.REST;

public class HorseApiRepository : ApiRepository<Horse, HorseModel>
{
    public HorseApiRepository(NHttpClient client)
        : base("horses", client) { }
}
