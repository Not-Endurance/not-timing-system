using Not.Injection;
using NTS.Domain.Core.Configuration;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Contexts;

public class CountriesContext : ICountriesContext
{

    public Task<IEnumerable<Country>> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Task.FromResult(StaticOptions.Countries.AsEnumerable());
        }
        var result = StaticOptions.Countries.Where(x => x.Name.Contains(term, StringComparison.InvariantCultureIgnoreCase));
        return Task.FromResult(result);
    }
}

public interface ICountriesContext : ISingletonService
{
    Task<IEnumerable<Country>> Search(string partialName);
}
