using Not.Blazor.Navigation;
using NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Competitions;

public partial class CompetitionUpdate
{
    CompetitionFormModel? _competition;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected override void OnInitialized()
    {
        _competition = Navigator.ConsumeParameter<CompetitionFormModel>();
    }
}
