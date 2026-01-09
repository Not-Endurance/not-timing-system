using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Aggregates;

namespace NTS.Storage.REST;

public class CountryRestApiRepository : RestApiRepository<Country>
{
    public CountryRestApiRepository(NHttpClient client)
        : base("countries", client) { }
}
