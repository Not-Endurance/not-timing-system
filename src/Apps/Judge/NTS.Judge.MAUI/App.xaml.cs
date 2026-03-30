#pragma warning disable CA1416 // Validate platform compatibility
using NTS.Judge.MAUI.Platforms.Services;

namespace NTS.Judge.MAUI;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(IMauiProcessService mauiProcessService)
    {
        InitializeComponent();
        mauiProcessService.SetAppName("NTS Judge");

        MainPage = new SplashPage();
    }
}
#pragma warning restore CA1416 // Validate platform compatibility
