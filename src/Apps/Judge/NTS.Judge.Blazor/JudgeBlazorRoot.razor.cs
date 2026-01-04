using System.Reflection;
using Not.Blazor.Components;

namespace NTS.Judge.Blazor;

public partial class JudgeBlazorRoot
{
    IEnumerable<Assembly> _routeAssemblies = [typeof(JudgeBlazorRoot).Assembly];
    NErrorBoundary _errorBoundary = default!;

    [Parameter]
    public Assembly Assembly { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }
}
