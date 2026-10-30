using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Console.Features.Setup.Combinations;

public class CombinationShellBehind : KrudShell<CombinationFormModel>
{
    /// <summary>
    /// How long a person stops typing before the registries are searched: the search of every Tenant is limited per account
    /// (ADR-0012), and a request for each key would spend it on half a name.
    /// </summary>
    protected const int SEARCH_DEBOUNCE_MILLISECONDS = 500;

    [Inject]
    IConsoleLookups Lookups { get; set; } = default!;

    protected async Task<IEnumerable<Athlete?>> SearchAthletesSafe(string term, CancellationToken _)
    {
        return (await Lookups.SearchAthletes(term)).Cast<Athlete?>();
    }

    protected async Task<IEnumerable<Horse?>> SearchHorsesSafe(string term, CancellationToken _)
    {
        return (await Lookups.SearchHorses(term)).Cast<Horse?>();
    }
}
