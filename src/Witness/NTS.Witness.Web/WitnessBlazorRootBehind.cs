using System.Reflection;
using Not.Application.RPC.SignalR;
using Not.Blazor.Components;
using Not.Filesystem;
using Not.Startup;
using Microsoft.AspNetCore.Components;

namespace NTS.Witness.Web;

public partial class WitnessBlazorRoot
{
    Assembly _appAssembly = typeof(Program).Assembly;
    IEnumerable<Assembly> _routeAssemblies = [typeof(WitnessBlazorRoot).Assembly];
    NErrorBoundary _errorBoundary = default!;

    [Inject] 
    public IEnumerable<IStartupInitializer> Initializers { get; set; } = default!;
    [Inject]
    public IRpcSocket RpcSocket { get; set; } = default!;

    protected override void OnInitialized()
    {
        FileContextHelper.ConfigureApplicationName("nts-witness");

        foreach (var initializer in Initializers)
        {
            initializer.RunAtStartup();
        }

        RpcSocket.Connect();
     }
}
