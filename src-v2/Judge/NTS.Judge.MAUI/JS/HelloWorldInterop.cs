using Microsoft.JSInterop;
using MudBlazor;
using Not.Blazor.Print;
using NTS.Judge.Ports;

namespace NTS.Judge.MAUI.JS;

public class HelloWorldInterop : IHelloWorldInterop, IPrintInterop
{
    private readonly IJSRuntime _jsRuntime;

    public HelloWorldInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task Hello()
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/hello-world.js");
        await module.InvokeVoidAsync("hello");
    }

    public async Task Print(MudThemeProvider mudThemeProvider)
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/print-custom.js");
        await module.InvokeVoidAsync("printCustom");
    }

    public async Task Print()
    {
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/print-custom.js");
        await module.InvokeVoidAsync("printCustom");
    }
}
