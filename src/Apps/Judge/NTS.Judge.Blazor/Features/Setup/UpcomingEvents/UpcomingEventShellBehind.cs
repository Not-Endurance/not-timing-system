using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents;

public class UpcomingEventShellBehind : KrudShell<UpcomingEventFormModel>
{
    [Inject]
    ISeeker<Country> Countries { get; set; } = default!;

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken ct)
    {
        return await Countries.Search(term, ct);
    }
}
