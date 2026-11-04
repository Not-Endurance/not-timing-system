using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Aggregates;

namespace NoTiming.Storage.REST;

public class CountryApiRepository : ApiRepository<Country, CountryModel>
{
    public CountryApiRepository(NHttpClient client)
        : base("countries", client) { }
}
