using MudBlazor;
using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

namespace NTS.Judge.Blazor.Features.Setup.Combinations;

public class CombinationShellBehind : KrudShell<CombinationFormModel>
{
    [Inject]
    ISettService<Athlete> AthletesService { get; set; } = default!;

    [Inject]
    ISettService<Horse> HorsesService { get; set; } = default!;

    protected async Task<IEnumerable<Athlete?>> SearchAthletesSafe(string term, CancellationToken _)
    {
        return Search(await AthletesService.ReadMany(), term);
    }

    protected async Task<IEnumerable<Horse?>> SearchHorsesSafe(string term, CancellationToken _)
    {
        return Search(await HorsesService.ReadMany(), term);
    }

    IEnumerable<T?> Search<T>(IEnumerable<T> values, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return values;
        }
        return values.Where(x =>
            x != null && x.ToString()!.Contains(term, StringComparison.InvariantCultureIgnoreCase)
        );
    }
}
