using Not.Application.Cache;
using Not.Application.Services;
using Not.Strings;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup.Services;

public class CountrySeeker : ISeeker<Country>
{
    readonly ICache<Country> _countriesCache;

    public CountrySeeker(ICache<Country> countriesCache)
    {
        _countriesCache = countriesCache;
    }

    public async Task<IEnumerable<Country>> Search(string term, CancellationToken _)
    {
        var items = await _countriesCache.List(); // TODO: convert to CountryService
        return items.Where(x => x.Name.NContains(term));
    }
}
