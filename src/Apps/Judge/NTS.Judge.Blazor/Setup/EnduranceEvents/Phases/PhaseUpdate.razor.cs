using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Phases;

public partial class PhaseUpdate
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
}
