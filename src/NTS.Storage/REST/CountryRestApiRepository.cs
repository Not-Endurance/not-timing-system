using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Shared;
using NTS.Domain.Aggregates;

namespace NTS.Storage.REST;

public class CountryRestApiRepository : RestApiRepository<Country, CountryModel>, ITransient
{
    public CountryRestApiRepository(NHttpClient client)
        : base("countries", client) { }
}
