using Not.Filesystem;
using Not.Startup;
using NTS.Judge.MAUI.Platforms.Services;

namespace NTS.Judge.MAUI;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(IEnumerable<IStartupInitializer> initializers, IMauiProcessService mauiProcessService)
    {
        try
        {
            InitializeComponent();
            mauiProcessService.SetAppName("NTS Judge");

            MainPage = new SplashPage();

            FileContextHelper.ConfigureApplicationName("nts");

            foreach (var initializer in initializers)
            {
                initializer.RunAtStartup();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }
}
