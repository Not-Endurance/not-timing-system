using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Features.Socket;

namespace NoTiming.Ui.Features.Core.Arrivelist;

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
