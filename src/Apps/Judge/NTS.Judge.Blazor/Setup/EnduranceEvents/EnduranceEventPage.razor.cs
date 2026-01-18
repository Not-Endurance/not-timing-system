using Not.Blazor.Navigation;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public partial class EnduranceEventPage
{
    UpcomingEventFormModel? _upcomingEvent;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    IRpcContext<UpcomingEvent> RpcContext { get; set; } = default!;

    protected override void OnInitialized()
    {
        _upcomingEvent = Navigator.ConsumeParameter<UpcomingEventFormModel>();
        // TODO: come up with somethig more elegant. Maybe pass aggregates in navigaion?
        RpcContext.Set(
            new UpcomingEvent(
                _upcomingEvent.Id,
                _upcomingEvent.Name,
                _upcomingEvent.Place,
                _upcomingEvent.Country,
                _upcomingEvent.FeiShowId,
                _upcomingEvent.FeiId,
                _upcomingEvent.FeiEventCode,
                _upcomingEvent.Competitions,
                _upcomingEvent.Officials,
                _upcomingEvent.Loops,
                _upcomingEvent.Combinations
            )
        );
    }
}
