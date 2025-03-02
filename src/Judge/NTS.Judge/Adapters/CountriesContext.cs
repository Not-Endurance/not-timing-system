using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Strings;
using NTS.Domain.Aggregates;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters;

public class CountriesContext : HttpCache<Country>, ICountryCache
{
    public CountriesContext(IRepository<Country> countries) : base(countries)
    {
    }

    public async Task<IEnumerable<Country>> Search(string term)
    {
        var countries = await List();

        return string.IsNullOrWhiteSpace(term)
            ? countries
            : countries.Where(x => x.Name.NContains(term));
    }
}
