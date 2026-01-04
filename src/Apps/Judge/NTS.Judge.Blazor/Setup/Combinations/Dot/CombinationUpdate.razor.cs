using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.Combinations.Dot;

public partial class CombinationUpdate
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
}
