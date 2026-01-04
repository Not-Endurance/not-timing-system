using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Lists.Ports;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.Combinations.Dot;

public partial class CombinationForm
{
    MudNumericField<int?> _numberField = default!;
    NAutocomplete<Athlete?> _athleteField = default!;
    NAutocomplete<Horse?> _horseField = default!;

    [Inject]
    IListBehind<Athlete> AthletesBehind { get; set; } = default!;

    [Inject]
    IListBehind<Horse> HorsesBehind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(AthletesBehind);
        await Observe(HorsesBehind);
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Combination.Number), () => _numberField);
        RegisterInjector(nameof(Combination.Athlete), () => _athleteField);
        RegisterInjector(nameof(Combination.Horse), () => _horseField);
    }

    Task<IEnumerable<Athlete?>> SearchAthletes(string term)
    {
        var result = Search(AthletesBehind.Items, term);
        return Task.FromResult(result);
    }

    Task<IEnumerable<Horse?>> SearchHorses(string term)
    {
        var result = Search(HorsesBehind.Items, term);
        return Task.FromResult(result);
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
