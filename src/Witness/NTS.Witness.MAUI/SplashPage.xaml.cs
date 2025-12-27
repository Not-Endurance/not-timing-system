namespace NTS.Witness;

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
        Application.Current!.MainPage = new MainPage();
    }
}
