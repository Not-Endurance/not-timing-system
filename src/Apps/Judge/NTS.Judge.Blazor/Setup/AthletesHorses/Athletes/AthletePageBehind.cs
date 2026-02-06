using Not.Blazor.Components;
using Not.Blazor.Navigation;
using NTS.Judge.Features.Setup.Athletes;

namespace NTS.Judge.Blazor.Setup.AthletesHorses.Athletes;

public class AthletePageBehind : NComponent
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected AthleteFormModel Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        Model = Navigator.ConsumeParameter<AthleteFormModel>();
    }
}
