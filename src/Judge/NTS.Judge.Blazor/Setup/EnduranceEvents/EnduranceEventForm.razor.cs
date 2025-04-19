using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Ports;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public partial class EnduranceEventForm
{
    MudTextField<string?> _placeField = default!;
    NAutocomplete<Country?> _countryField = default!;

    [Inject]
    ISeeker<Country> Countries { get; set; } = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(UpcomingEvent.Place), () => _placeField);
        RegisterInjector(nameof(UpcomingEvent.Country), () => _countryField);
    }
}
