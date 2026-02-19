using System.Reflection;
using Microsoft.AspNetCore.Components.Web;

namespace NTS.Judge.Blazor;

public partial class JudgeBlazorRoot
{
    IEnumerable<Assembly> _routeAssemblies = [typeof(JudgeBlazorRoot).Assembly];
    ErrorBoundary _errorBoundary = default!;

    [Parameter]
    public Assembly Assembly { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }
}
