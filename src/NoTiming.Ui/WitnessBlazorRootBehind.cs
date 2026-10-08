using System.Reflection;
using Microsoft.AspNetCore.Components.Routing;
using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Features.Lazy;

namespace NoTiming.Ui;

public class WitnessBlazorRootBehind : NComponent
{
    [Inject]
    protected ILazyAssemblies LazyAssemblies { get; set; } = default!;

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;

    /// <summary>
    /// Fetches the assemblies a path needs before the router looks for its page (<see cref="LazyRoutes"/>): the pages of the
    /// Console are not part of what is downloaded to open the app.
    /// </summary>
    protected async Task LoadAssembliesFor(NavigationContext context)
    {
        try
        {
            await LazyAssemblies.LoadFor(context.Path);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
