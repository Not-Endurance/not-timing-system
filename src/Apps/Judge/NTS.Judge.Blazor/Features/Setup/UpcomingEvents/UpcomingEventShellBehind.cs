using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Contracts.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents;

public class UpcomingEventShellBehind : KrudShell<UpcomingEventFormModel>
{
    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken ct)
    {
        return await Lookups.SearchCountries(term, ct);
    }
}
