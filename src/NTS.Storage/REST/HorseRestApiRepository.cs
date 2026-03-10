using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class HorseRestApiRepository : RestApiRepository2<Horse, HorseModel>, ITransient
{
    public HorseRestApiRepository(NHttpClient client)
        : base("horses", client) { }
}
