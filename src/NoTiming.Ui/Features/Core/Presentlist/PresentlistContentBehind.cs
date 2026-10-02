using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Features.Socket;

namespace NoTiming.Ui.Features.Core.Presentlist;

public class PresentlistContentBehind : NComponent
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
