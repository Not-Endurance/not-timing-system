using Not.Blazor.Navigation.Abstractions;
using NTS.Judge.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents;

public class UpcomingEventContentBehind : SetupFormContent<UpcomingEventFormModel>
{
    [Inject]
    ISelectedUpcomingEventContext SelectedContext { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            base.OnInitialized();
            SelectedContext.Event = Model.MapToEntity();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
