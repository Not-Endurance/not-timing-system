using Not.Injection;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Blazor.Ports;

public interface ICountriesContext : ISingleton
{
    Task<IEnumerable<Country>> Search(string partialName);
}
