using Microsoft.Extensions.Localization;
using NC.Localization;

namespace NoTiming.Judge.Blazor.Layout;

public class PrintLayoutBehind : LayoutComponentBase, IDisposable
{
    [Inject]
    IStringLocalizer? StringLocalizer { get; set; }

    protected override void OnInitialized()
    {
        LocalizationHelper.Configure(StringLocalizer);
    }

    public void Dispose()
    {
        LocalizationHelper.Clear(StringLocalizer);
    }
}
