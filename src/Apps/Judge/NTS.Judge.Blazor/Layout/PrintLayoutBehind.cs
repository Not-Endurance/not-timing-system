using Microsoft.Extensions.Localization;
using Not.Localization;

namespace NTS.Judge.Blazor.Layout;

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
