using Not.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Witness.Contracts.Features.Profile;

namespace NTS.Witness.Blazor.Features.Profile;

public class ProfileContentBehind : NStatefulComponent
{
    [Inject]
    IWitnessProfileContext ProfileContext { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected WitnessProfileFormModel Model { get; private set; } = new();
    protected bool CanSave => WitnessProfilePolicy.IsComplete(Model);

    protected override async Task OnInitializedAsync()
    {
        await Observe(ProfileContext);
        Model = ProfileContext.CreateFormModel();
    }

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken ct)
    {
        return (await ProfileContext.SearchCountries(term, ct)).Cast<Country?>();
    }

    protected async Task Save()
    {
        try
        {
            var redirectHome = ProfileContext.RequiresProfileCompletion;
            await ProfileContext.Save(Model);
            Model = ProfileContext.CreateFormModel();

            if (redirectHome)
            {
                Navigator.NavigateTo(Routes.HOME);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
