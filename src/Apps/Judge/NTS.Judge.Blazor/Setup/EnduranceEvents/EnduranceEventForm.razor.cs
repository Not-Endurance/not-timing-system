using MudBlazor;
using Not.Application.Services;
using Not.Blazor.Components;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public partial class EnduranceEventForm
{
    MudTextField<string?> _placeField = default!;
    MudTextField<string?> _nameField = default!;
    NAutocomplete<Country?> _countryField = default!;

    [Inject]
    ISeeker<Country> Countries { get; set; } = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(UpcomingEvent.Place), () => _placeField);
        RegisterInjector(nameof(UpcomingEvent.Name), () => _nameField);
        RegisterInjector(nameof(UpcomingEvent.Country), () => _countryField);
    }
}
