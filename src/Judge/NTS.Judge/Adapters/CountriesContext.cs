using Not.Application.HTTP;
using Not.Serialization;
using Not.Strings;
using NTS.Domain.Aggregates;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters;

public class CountriesContext : HttpCache<Country>, ICountryCache
{
    readonly NHttpClient _client;

    public CountriesContext(NHttpClient client)
    {
        _client = client;
    }

    protected override async Task<IEnumerable<Country>> FetchItems()
    {
        var resposne = await _client.Get("countries");
        return resposne.FromJson<IEnumerable<Country>>();
    }

    public async Task<IEnumerable<Country>> Search(string term)
    {
        var countries = await List();

        return string.IsNullOrWhiteSpace(term)
            ? countries
            : countries.Where(x => x.Name.NContains(term));
    }
}
