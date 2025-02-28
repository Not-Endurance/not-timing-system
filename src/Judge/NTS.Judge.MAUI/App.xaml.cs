using Not.Filesystem;
using Not.Startup;

namespace NTS.Judge.MAUI;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(IEnumerable<IStartupInitializer> initializers)
    {
        InitializeComponent();

        MainPage = new MainPage();

        FileContextHelper.ConfigureApplicationName("nts");

        foreach (var initializer in initializers)
        {
            initializer.RunAtStartup();
        }
    }
}
