using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Aggregates;

namespace NTS.Storage.REST;

public class CountryApiRepository : ApiRepository<Country, CountryModel>
{
    public CountryApiRepository(NHttpClient client)
        : base("countries", client) { }
}
