using MudBlazor;
using Not.Blazor.Components;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public partial class EnduranceEventForm
{
    MudTextField<string?> _placeField = default!;
    NAutocomplete<Country?> _countryField = default!;

    [Inject]
    ICountryCache Countries { get; set; } = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(EnduranceEvent.Place), () => _placeField);
        RegisterInjector(nameof(EnduranceEvent.Country), () => _countryField);
    }
}
