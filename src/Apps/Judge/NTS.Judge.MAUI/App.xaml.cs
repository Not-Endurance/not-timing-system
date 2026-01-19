using Not.Filesystem;
using Not.Startup;
using NTS.Judge.MAUI.Platforms.Services;

namespace NTS.Judge.MAUI;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(IEnumerable<IStartupInitializer> initializers, IMauiProcessService mauiProcessService)
    {
        InitializeComponent();
        mauiProcessService.SetAppName("NTS Judge");

        Windows[0].Page = new SplashPage();

        FileContextHelper.ConfigureApplicationName("nts");

        foreach (var initializer in initializers)
        {
            initializer.RunAtStartup();
        }
    }
}
