#pragma warning disable CA1416 // Validate platform compatibility

namespace NTS.Judge.MAUI;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
        Loaded += SplashPage_Loaded;
    }

    async void SplashPage_Loaded(object? sender, EventArgs e)
    {
        await Task.Delay(2000);
        Microsoft.Maui.Controls.Application.Current!.MainPage = new MainPage();
    }
}
#pragma warning restore CA1416 // Validate platform compatibility
