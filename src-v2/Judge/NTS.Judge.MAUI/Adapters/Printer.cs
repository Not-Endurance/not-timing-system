using BlazorTemplater;
using Not.Blazor.Navigation;
using NTS.Judge.Blazor.Pages;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.MAUI.Adapters;

public class Printer : IPrinter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigator _navigator;

    public Printer(IServiceProvider serviceProvider, INavigator navigator)
    {
        _serviceProvider = serviceProvider;
        _navigator = navigator;
    }

    public async Task NavigateToPrint()
    {
        var mudCss = await File.ReadAllTextAsync("./wwwroot/_content/MudBlazor/MudBlazor.min.css");
        _navigator.NavigateTo(Endpoints.PRINT_PAGE, mudCss);
    }
}
