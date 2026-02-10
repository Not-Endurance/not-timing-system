using NTS.Application.Socket;
using NTS.Judge.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents;

public class UpcomingEventContentBehind : SetupFormContent<UpcomingEventFormModel>
{
    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            base.OnInitialized();
            SocketService.Connect(Model.MapToEntity());
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
