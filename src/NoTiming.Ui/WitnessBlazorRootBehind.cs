using System.Reflection;
using Not.Blazor.Components.Abstractions;

namespace NoTiming.Ui;

public class WitnessBlazorRootBehind : NComponent
{
    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;
}
