using Not.Application.Services;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Setup.Services;

public class LanguageSeeker : ILanguageSeeker
{
    readonly ISeeker<Country> _countrySeeker;

    public LanguageSeeker(ISeeker<Country> countrySeeker)
    {
        _countrySeeker = countrySeeker;
    }

    public async Task<IEnumerable<Country?>> SearchLocalizedCountries(string term, CancellationToken ct)
    {
        var foundCountries = await _countrySeeker.Search(term, ct);
        return foundCountries.Where(x => x.Locale != null);
    }
}
