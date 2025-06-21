using Not.Filesystem;
using Not.Startup;

namespace NTS.Judge.MAUI;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(IEnumerable<IStartupInitializer> initializers)
    {
        InitializeComponent();
#if WINDOWS
        Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
        {
            var nativeWindow = handler.PlatformView;
            nativeWindow.Title = "NTS Judge";
        });
#endif
        MainPage = new SplashPage();

        FileContextHelper.ConfigureApplicationName("nts");

        foreach (var initializer in initializers)
        {
            initializer.RunAtStartup();
        }
    }
}
