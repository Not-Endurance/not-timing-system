using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Officials;

public partial class OfficialUpdate
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
}
