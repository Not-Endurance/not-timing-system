using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Participations;

public partial class ParticipationUpdate
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
}
