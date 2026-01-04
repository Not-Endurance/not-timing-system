using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.AthletesHorses.Horses;

public partial class HorseUpdate
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
}
