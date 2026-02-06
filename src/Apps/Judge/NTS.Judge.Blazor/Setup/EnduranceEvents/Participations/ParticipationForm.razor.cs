using MudBlazor;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Blazor.Components.Input;
using Not.Strings;
using NTS.Blazor.Constants;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Participations;

public partial class ParticipationForm
{
    static readonly PatternMask TIME_MASK = new(Masks.MINUTES_TIME_MASK_FORMAT);

    NAutocomplete<Combination> _combinationField = default!;
    MudPicker<TimeSpan?> _timeField = default!;
    MudNumericField<double?> _maxSpeedOverrideField = default!;
    MudNumericField<double?> _minSpeedOverrideField = default!;
    NSelect<ParticipationCategory?> _categoryField = default!;

    [Inject]
    IListBehind<Combination> Behind { get; set; } = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Participation.Combination), () => _combinationField);
        RegisterInjector(nameof(Participation.StartTimeOverride), () => _timeField);
        RegisterInjector(nameof(Participation.MaxSpeedOverride), () => _maxSpeedOverrideField);
        RegisterInjector(nameof(Participation.MinSpeedOverride), () => _minSpeedOverrideField);
        RegisterInjector(nameof(Participation.Category), () => _categoryField);
    }

    async Task<IEnumerable<Combination>> SearchCombinations(string term, CancellationToken _)
    {
        return Search(await Behind.ReadMany(), term);
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
