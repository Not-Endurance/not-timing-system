using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class HorseApiRepository : ApiRepository<Horse, HorseModel>
{
    public HorseApiRepository(NHttpClient client)
        : base("horses", client) { }
}
