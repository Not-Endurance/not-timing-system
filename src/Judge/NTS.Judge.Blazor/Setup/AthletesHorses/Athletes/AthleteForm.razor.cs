using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Ports;
using Not.Structures;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.AthletesHorses.Athletes;

public partial class AthleteForm
{
    // TODO: Introduce NotText variants for consistency
    MudTextField<string?> _nameField = default!;
    MudTextField<string?> _feiIdField = default!;
    NAutocomplete<Country?> _countryField = default!;
    NAutocomplete<Club?> _clubField = default!;
    NSelect<AthleteCategory> _categoryField = default!;

    [Inject]
    ISeeker<Country> Countries { get; set; } = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Athlete.Names), () => _nameField);
        RegisterInjector(nameof(Athlete.Country), () => _countryField);
        RegisterInjector(nameof(Athlete.Club), () => _clubField);
        RegisterInjector(nameof(Athlete.FeiId), () => _feiIdField);
        RegisterInjector(nameof(Athlete.Category), () => _categoryField);
    }
}
