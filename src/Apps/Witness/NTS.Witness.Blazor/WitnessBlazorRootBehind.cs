using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Not.Blazor.Components;

namespace NTS.Witness.Blazor;

public class WitnessBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> _routeAssemblies = [typeof(WitnessBlazorRoot).Assembly];
    protected ErrorBoundary _errorBoundary = default!;

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;
}
