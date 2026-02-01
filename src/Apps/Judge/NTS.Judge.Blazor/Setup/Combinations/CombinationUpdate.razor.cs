using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.Combinations;

public partial class CombinationUpdate
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
}
