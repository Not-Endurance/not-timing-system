using Not.Application.Cache;
using Not.Blazor.Ports;
using Not.Strings;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Setup.Seeker;

public class CountrySeeker : ISeeker<Country>
{
    readonly ICache<Country> _countriesCache;

    public CountrySeeker(ICache<Country> countriesCache)
    {
        _countriesCache = countriesCache;
    }

    public async Task<IEnumerable<Country>> Search(string term)
    {
        var items = await _countriesCache.List(); // TODO: convert to CountryBehind
        return items.Where(x => x.Name.NContains(term));
    }
}
