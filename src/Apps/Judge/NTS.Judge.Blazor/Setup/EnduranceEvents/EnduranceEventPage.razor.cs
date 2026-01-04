using Not.Blazor.Navigation;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public partial class EnduranceEventPage
{
    EnduranceEventFormModel? _upcomingEvent;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected override void OnInitialized()
    {
        _upcomingEvent = Navigator.ConsumeParameter<EnduranceEventFormModel>();
    }
}
