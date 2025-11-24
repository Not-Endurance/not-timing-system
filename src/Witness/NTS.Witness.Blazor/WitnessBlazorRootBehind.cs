using System.Reflection;
using Not.Application.RPC.SignalR;
using Not.Blazor.Components;
using Not.Filesystem;
using Not.Startup;
using NTS.Witness.Blazor.Components.Pages;

namespace NTS.Witness.Blazor;

public class WitnessBlazorRootBehind : NComponent
{
    protected IEnumerable<Assembly> _routeAssemblies = [typeof(WitnessBlazorRoot).Assembly, typeof(Performance).Assembly, typeof(WitnessPage).Assembly];
    protected NErrorBoundary _errorBoundary = default!;

    [Inject]
    public IEnumerable<IStartupInitializer> Initializers { get; set; } = default!;

    [Inject]
    public IRpcSocket RpcSocket { get; set; } = default!;

    [Parameter]
    public Assembly Assembly { get; set; } = default!;

    protected override void OnInitialized()
    {
        Console.WriteLine("!!!!!!!!!!!!ÌÐÚÚÑÍÈ ÊÓÐÎÂÅ!!!!!!!!!!!!!!");
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Console.WriteLine("ASM: " + asm.FullName);
        }
        FileContextHelper.ConfigureApplicationName("nts-witness");

        foreach (var initializer in Initializers)
        {
            initializer.RunAtStartup();
        }

        RpcSocket.Connect();
    }
}
