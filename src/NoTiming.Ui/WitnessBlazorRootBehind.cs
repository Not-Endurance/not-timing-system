using System.Reflection;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Components.Abstractions;

namespace NoTiming.Ui;

public class WitnessBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> RouteAssemblies { get; } = [typeof(AuthenticationContents).Assembly];

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;
}
