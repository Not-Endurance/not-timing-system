using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Components.Abstractions;

namespace NoTiming.Ui.Blazor;

public class UiBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> RouteAssemblies { get; } =
        [typeof(UiBlazorRootBehind).Assembly, typeof(AuthenticationContents).Assembly];

    protected ErrorBoundary ErrorBoundary { get; set; } = default!;

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;
}
