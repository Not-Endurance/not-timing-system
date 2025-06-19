using Not.Application.RPC.SignalR;
using Not.Filesystem;
using Not.Startup;

namespace NTS.Witness;

public partial class App : Application
{
    public App(IEnumerable<IStartupInitializer> initializers, IRpcSocket rpcSocket)
    {
        InitializeComponent();

        MainPage = new SplashPage();

        FileContextHelper.ConfigureApplicationName("nts-witness");

        foreach (var initializer in initializers)
        {
            initializer.RunAtStartup();
        }
        rpcSocket.Connect();
    }
}
