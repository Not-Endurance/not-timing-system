using Not.Application.HTTP;
using Not.Async;
using Not.Safe;
using Not.Serialization;
using Not.Strings;
using NTS.Domain.Aggregates;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters;

public class CountriesContext : ICountriesContext
{
    readonly NHttpClient _client;
    readonly SemaphoreSlim _semaphore = new(1);
    List<Country> _countries = [];

    public CountriesContext(NHttpClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<Country>> Search(string term)
    {
        var result = await SafeHelper.Run(async () =>
        {
            if (_countries.Count == 0)
            {
                await _semaphore.WaitAsync();
                try
                {
                    _countries = await FetchCountries().ToList();
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            return string.IsNullOrWhiteSpace(term)
                ? _countries.AsEnumerable()
                : _countries.Where(x => x.Name.NContains(term));
        });

        return result?.Where(x => x != null) ?? [];
    }

    async Task<IEnumerable<Country>> FetchCountries()
    {
        var resposne = await _client.Get("countries");
        return resposne.FromJson<IEnumerable<Country>>();
    }
}
