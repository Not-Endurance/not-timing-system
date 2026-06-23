using System.Reflection;
using Not.Blazor.Components.Abstractions;

namespace NTS.Judge.Blazor;

public class JudgeBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> RouteAssemblies { get; } = [typeof(JudgeBlazorRootBehind).Assembly];

    [Parameter]
    public Assembly Assembly { get; set; } = default!;
}
