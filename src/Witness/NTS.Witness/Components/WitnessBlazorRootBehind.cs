using System.Reflection;
using Not.Blazor.Components;

namespace NTS.Witness.Components;

public partial class WitnessBlazorRoot
{
    IEnumerable<Assembly> _routeAssemblies = [typeof(WitnessBlazorRoot).Assembly];
    NErrorBoundary _errorBoundary = default!;

    [Parameter]
    public Assembly Assembly { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }
}
