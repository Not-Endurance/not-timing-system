using NC.Application.Environments;
using NoTiming.Application.Contracts;

namespace NoTiming.Ui.Blazor.Layout;

public class MainLayoutBehind : LayoutComponentBase
{
    [Inject]
    NEnvironment Environment { get; set; } = default!;

    protected string LayoutWatermark =>
        NoTimingClientDisplayFormatter.FormatTitle(ApplicationConstants.NO_TIMING_SYSTEM, Environment);
}
