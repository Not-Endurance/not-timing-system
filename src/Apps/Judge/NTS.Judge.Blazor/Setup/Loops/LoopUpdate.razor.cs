using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.Loops;

public partial class LoopUpdate
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
}
