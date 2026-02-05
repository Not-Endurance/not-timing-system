using MudBlazor;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Blazor.Components.Input;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Blazor.Setup.Combinations;

public partial class CombinationForm
{
    MudNumericField<int?> _numberField = default!;
    NAutocomplete<Athlete?> _athleteField = default!;
    NAutocomplete<Horse?> _horseField = default!;

    [Inject]
    IListBehind<Athlete> AthletesBehind { get; set; } = default!;

    [Inject]
    IListBehind<Horse> HorsesBehind { get; set; } = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Combination.Number), () => _numberField);
        RegisterInjector(nameof(Combination.Athlete), () => _athleteField);
        RegisterInjector(nameof(Combination.Horse), () => _horseField);
    }

    async Task<IEnumerable<Athlete?>> SearchAthletes(string term, CancellationToken _)
    {
        return Search(await AthletesBehind.ReadMany(), term);
    }

    async Task<IEnumerable<Horse?>> SearchHorses(string term, CancellationToken _)
    {
        return Search(await HorsesBehind.ReadMany(), term);
    }

    // TODO: extract search functionality somehow, because ToString() should be identical (maybe ToString should be configurable)
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
