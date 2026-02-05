using Not.Injection;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup.Services;

public interface ILanguageSeeker : ITransient
{
    Task<IEnumerable<Country?>> SearchLocalizedCountries(string term, CancellationToken _);
}
