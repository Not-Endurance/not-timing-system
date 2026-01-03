using System.Reflection;
using Not.Blazor.Components;

namespace NTS.Witness.Blazor;

public class WitnessBlazorRootBehind : NBehind
{
    protected IEnumerable<Assembly> _routeAssemblies = [typeof(WitnessBlazorRoot).Assembly];
    protected NErrorBoundary _errorBoundary = default!;

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;
}
