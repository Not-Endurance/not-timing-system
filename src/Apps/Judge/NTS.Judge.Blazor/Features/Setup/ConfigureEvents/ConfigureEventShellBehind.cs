using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents;

namespace NTS.Judge.Blazor.Features.Setup.ConfigureEvents;

public class ConfigureEventShellBehind : KrudShell<ConfigureEventFormModel>
{
    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken ct)
    {
        return await Lookups.SearchCountries(term, ct);
    }
}
