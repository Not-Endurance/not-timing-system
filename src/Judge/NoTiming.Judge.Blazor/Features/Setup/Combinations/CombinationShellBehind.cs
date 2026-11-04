using MudBlazor;
using NC.Krud.Blazor.Components.Abstractions;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Combinations;

namespace NoTiming.Judge.Blazor.Features.Setup.Combinations;

public class CombinationShellBehind : KrudShell<CombinationFormModel>
{
    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected async Task<IEnumerable<Athlete?>> SearchAthletesSafe(string term, CancellationToken _)
    {
        return await Lookups.SearchAthletes(term, CancellationToken.None);
    }

    protected async Task<IEnumerable<Horse?>> SearchHorsesSafe(string term, CancellationToken _)
    {
        return await Lookups.SearchHorses(term, CancellationToken.None);
    }
}
