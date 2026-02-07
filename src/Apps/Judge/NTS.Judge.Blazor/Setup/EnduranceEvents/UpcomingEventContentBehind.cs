using Not.Blazor.Navigation;
using NTS.Application.SignalR;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents;

public class UpcomingEventContentBehind : SetupFormContent<UpcomingEventFormModel>
{
    [Inject]
    IGroupSocketContext<UpcomingEvent> RpcContext { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            base.OnInitialized();
            RpcContext.Connect(Model.MapToEntity());
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
