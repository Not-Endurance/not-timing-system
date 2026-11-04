namespace NoTiming.Judge.Blazor.Layout;

public class JudgeMainLayoutBehind : LayoutComponentBase
{
    [Inject]
    NEnvironment Environment { get; set; } = default!;

    protected string LayoutWatermark =>
        NoTimingClientDisplayFormatter.FormatTitle(ApplicationConstants.Apps.JUDGE, Environment);
}
