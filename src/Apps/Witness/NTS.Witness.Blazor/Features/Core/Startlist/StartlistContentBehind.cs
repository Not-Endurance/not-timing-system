using Not.Blazor.Components.Abstractions;
using NTS.Witness.Blazor.Features.Socket;

namespace NTS.Witness.Blazor.Features.Core.Startlist;

public class StartlistContentBehind : NComponent
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
