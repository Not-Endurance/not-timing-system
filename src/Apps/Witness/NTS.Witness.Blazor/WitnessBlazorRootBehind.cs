using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor;

public class WitnessBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> RouteAssemblies { get; } =
    [
        typeof(WitnessBlazorRootBehind).Assembly,
        typeof(AuthenticationContents).Assembly,
    ];

    protected ErrorBoundary ErrorBoundary { get; set; } = default!;

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;
}
