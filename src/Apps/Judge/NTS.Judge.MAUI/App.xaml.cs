#pragma warning disable CA1416 // Validate platform compatibility
using Not.Startup;
using NTS.Judge.MAUI.Platforms.Services;

namespace NTS.Judge.MAUI;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(IEnumerable<IStartupInitializer> initializers, IMauiProcessService mauiProcessService)
    {
        InitializeComponent();
        mauiProcessService.SetAppName("NTS Judge");

        MainPage = new SplashPage();

        foreach (var initializer in initializers)
        {
            initializer.RunAtStartup();
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility

