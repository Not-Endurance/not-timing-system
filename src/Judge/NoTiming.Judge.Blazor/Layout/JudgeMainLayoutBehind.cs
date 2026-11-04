namespace NTS.Judge.Blazor.Layout;

public class JudgeMainLayoutBehind : LayoutComponentBase
{
    [Inject]
    NEnvironment Environment { get; set; } = default!;

    protected string LayoutWatermark =>
        NtsClientDisplayFormatter.FormatTitle(ApplicationConstants.Apps.JUDGE, Environment);
}
