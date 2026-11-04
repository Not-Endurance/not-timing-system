using Not.Application.Environments;
using NTS.Application.Contracts;

namespace NoTiming.Ui.Blazor.Layout;

public class MainLayoutBehind : LayoutComponentBase
{
    [Inject]
    NEnvironment Environment { get; set; } = default!;

    protected string LayoutWatermark =>
        NtsClientDisplayFormatter.FormatTitle(ApplicationConstants.NO_TIMING_SYSTEM, Environment);
}
