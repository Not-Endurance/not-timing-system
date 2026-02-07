using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public class UpcomingEventShellBehind : KrudShell<UpcomingEventFormModel>
{
    [Inject]
    ISeeker<Country> Countries { get; set; } = default!;

    protected async Task<IEnumerable<Country?>> SearchCountries(string term, CancellationToken ct)
    {
        return await Countries.Search(term, ct);
    }
}
