using System.Globalization;
using Not.Notify;

namespace NTS.Judge.Blazor.Demos;

public partial class LocalizationDemo
{
    string _polite = "";

    protected List<string> Strings { get; set; } = [];

    protected async Task ChangeCulture(string name)
    {
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(name);
        await InvokeAsync(StateHasChanged);
    }

    protected async Task SayPolite()
    {
        _polite = StringLocalizer[LocalizationTestService.Polite()];
        await InvokeAsync(StateHasChanged);
    }

    protected void Test()
    {
        var message = string.Format("TEST_WITH__VALUE_C", "test");
        NotifyHelper.Success(message);
    }

    protected void Test2()
    {
        var message = string.Format(StringLocalizer["TEST_WITH__VALUE_C"], StringLocalizer["TEST"]);
        NotifyHelper.Success(message);
    }

    protected async void Rude()
    {
        var item = LocalizationTestService.Rude();
        Strings.Add(item);
        await InvokeAsync(StateHasChanged);
    }
}
