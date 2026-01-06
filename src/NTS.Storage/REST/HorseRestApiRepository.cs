using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class HorseRestApiRepository : RestApiRepository<Horse>
{
    public HorseRestApiRepository(NHttpClient client)
        : base("horses", client) { }
}
