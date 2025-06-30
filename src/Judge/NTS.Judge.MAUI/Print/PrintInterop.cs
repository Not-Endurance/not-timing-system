using Microsoft.JSInterop;
using Not.Blazor.Ports;

namespace NTS.Judge.MAUI.Print;

public class PrintInterop : IPrintInterop
{
    readonly IJSRuntime _jsRuntime;

    public PrintInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    // The JS implementation currently depends on using MudBlazor UI in order to locate the main content element
    // I've left it as such as there isn't any realistic chance of dropping Mud any time soon
    public async Task OpenPrintDialog()
    {
        await using var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/print-custom.js");
        await module.InvokeVoidAsync("printCustom");
    }
}
