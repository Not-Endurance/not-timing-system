using Not.Application.HTTP;
using NTS.Domain.Aggregates;

namespace NTS.Judge.HTTP;

public class CountryHttpRepository : HttpRepository<Country>
{
    public CountryHttpRepository(NHttpClient client)
        : base("countries", client) { }
}
