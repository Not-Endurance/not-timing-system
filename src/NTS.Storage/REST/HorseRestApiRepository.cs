using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class HorseRestApiRepository : RestApiRepository<Horse, HorseModel>, ITransient
{
    public HorseRestApiRepository(NHttpClient client)
        : base("horses", client) { }
}
