using System.Reflection;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor;

public class WitnessBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> RouteAssemblies { get; } =
        [typeof(WitnessBlazorRootBehind).Assembly, typeof(AuthenticationContents).Assembly];

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;
}
