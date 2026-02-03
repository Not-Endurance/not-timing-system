using MudBlazor;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.Athletes;

namespace NTS.Judge.Blazor.Setup.AthletesHorses.Athletes;

public class AthleteFormBehind : KrudFormContainer<AthleteFormModel>
{
    [Inject]
    IUpdateBehind<AthleteFormModel> Service { get; set; } = default!;

    protected MudTextField<string?> NameField { get; set; } = default!;
    protected MudTextField<string?> FeiIdField { get; set; } = default!;
    protected NAutocomplete<Country?> CountryField { get; set; } = default!;
    protected NAutocomplete<Club?> ClubField { get; set; } = default!;

    [Inject]
    protected ISeeker<Country> Countries { get; set; } = default!;

    [Parameter]
    public AthleteFormModel Model { get; set; } = default!;

    protected async Task Update(AthleteFormModel model)
    {
        try
        {
            await Service.Update(model);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Athlete.Names), () => NameField);
        RegisterInjector(nameof(Athlete.Country), () => CountryField);
        RegisterInjector(nameof(Athlete.Club), () => ClubField);
        RegisterInjector(nameof(Athlete.FeiId), () => FeiIdField);
    }
}
