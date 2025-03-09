using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Strings;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Contestants;

public partial class ParticipationForm
{
    NAutocomplete<Combination> _combinationField = default!;
    NSwitch _isNotRankedField = default!;
    MudPicker<TimeSpan?> _timeField = default!;
    MudNumericField<double?> _maxSpeedOverrideField = default!;
    bool _overrideMaxSpeed;
    bool _overrideStartTime;

    [Inject]
    IListBehind<Combination> Behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Participation.Combination), () => _combinationField);
        RegisterInjector(nameof(Participation.IsNotRanked), () => _isNotRankedField);
        RegisterInjector(nameof(Participation.StartTimeOverride), () => _timeField);
        RegisterInjector(nameof(Participation.MaxSpeedOverride), () => _maxSpeedOverrideField);
    }

    Task<IEnumerable<Combination>> SearchCombinations(string term)
    {
        var result = Search(Behind.Items, term);
        return Task.FromResult(result);
    }

    // TODO: extract search functionality somehow, because ToString() should be identical (maybe ToString should be configurable)
    IEnumerable<T> Search<T>(IEnumerable<T> values, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return values;
        }
        return values.Where(x => x != null && StringExtensions.NContains(x.ToString()!, term));
    }
}
