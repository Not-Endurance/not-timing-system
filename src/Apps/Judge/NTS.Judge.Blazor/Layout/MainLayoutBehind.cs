namespace NTS.Judge.Blazor.Layout;

public class MainLayoutBehind : LayoutComponentBase
{
    [Inject]
    NEnvironment Environment { get; set; } = default!;

    protected string LayoutWatermark =>
        NtsClientDisplayFormatter.FormatTitle(ApplicationConstants.Apps.JUDGE, Environment);
}
