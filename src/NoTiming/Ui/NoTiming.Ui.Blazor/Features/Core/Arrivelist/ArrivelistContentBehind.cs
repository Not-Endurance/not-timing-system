using NC.Blazor.Components.Abstractions;
using NoTiming.Ui.Blazor.Features.Socket;

namespace NoTiming.Ui.Blazor.Features.Core.Arrivelist;

public class ArrivelistContentBehind : NComponent
{
    [Inject]
    BlazorSocketService BlazorSocketService { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await BlazorSocketService.EnsureConnected();
        }
    }
}
