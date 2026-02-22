using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Not.Blazor.Components.Abstractions;

namespace NTS.Judge.Blazor;

public class JudgeBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> RouteAssemblies { get; } = [typeof(JudgeBlazorRootBehind).Assembly];

    protected ErrorBoundary ErrorBoundary { get; set; } = default!;

    [Parameter]
    public Assembly Assembly { get; set; } = default!;
}
