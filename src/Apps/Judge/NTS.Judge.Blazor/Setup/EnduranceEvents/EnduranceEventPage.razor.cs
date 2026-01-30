using Not.Blazor.Navigation;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public partial class EnduranceEventPage
{
    UpcomingEventFormModel? _upcomingEvent;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    ISocketContext<UpcomingEvent> RpcContext { get; set; } = default!;

    protected override void OnInitialized()
    {
        _upcomingEvent = Navigator.ConsumeParameter<UpcomingEventFormModel>();
        // TODO: come up with somethig more elegant. Maybe pass aggregates in navigaion?
        RpcContext.Connect(
            new UpcomingEvent(
                _upcomingEvent.Name,
                _upcomingEvent.Place,
                _upcomingEvent.Country,
                _upcomingEvent.FeiShowId,
                _upcomingEvent.FeiId,
                _upcomingEvent.FeiEventCode,
                _upcomingEvent.Competitions,
                _upcomingEvent.Officials,
                _upcomingEvent.Loops,
                _upcomingEvent.Combinations,
                _upcomingEvent.Id
            )
        );
    }
}
