using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Aggregates;

namespace NTS.Judge.HTTP;

public class CountryHttpRepository : RestApiRepository<Country>
{
    public CountryHttpRepository(NHttpClient client)
        : base("countries", client) { }
}
