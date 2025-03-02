using Not.Injection;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.StaticOptions;

namespace NTS.Judge.Blazor.Shared.Contexts;

public class CountriesContext : ICountriesContext
{
    public Task<IEnumerable<Country>> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            var countries = StaticOption.Countries.AsEnumerable();
            return Task.FromResult(countries);
        }
        var result = StaticOption.Countries.Where(x =>
            x.Name.Contains(term, StringComparison.InvariantCultureIgnoreCase)
        );
        return Task.FromResult(result);
    }
}

public interface ICountriesContext : ISingleton
{
    Task<IEnumerable<Country>> Search(string partialName);
}
