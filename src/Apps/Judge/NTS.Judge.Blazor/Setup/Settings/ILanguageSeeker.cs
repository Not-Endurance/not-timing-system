using Not.Injection;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Blazor.Setup.Settings;

public interface ILanguageSeeker : ITransient
{
    Task<IEnumerable<Country?>> SearchLocalizedCountries(string term);
}
