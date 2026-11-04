using NC.Krud.Blazor.Components.Abstractions;
using NoTiming.Domain.Aggregates;
using NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents;

namespace NoTiming.Judge.Blazor.Features.Setup.ConfigureEvents;

public class ConfigureEventShellBehind : KrudShell<ConfigureEventFormModel>
{
    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken ct)
    {
        return await Lookups.SearchCountries(term, ct);
    }
}
