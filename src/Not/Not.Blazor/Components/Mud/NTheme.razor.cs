using MudBlazor;

namespace Not.Blazor.Components.Mud;

public partial class NTheme
{
    MudTheme Theme { get; set; } = default!;

    [Parameter]
    public MudThemeProvider ThemeProvider { get; set; } = default!;

    [Parameter]
    public bool DialogCloseOnEscapeKey { get; set; } = true;

    [Parameter]
    public bool DialogCloseButton { get; set; } = true;

    [Parameter]
    public MaxWidth DialogMaxWidth { get; set; } = MaxWidth.Small;

    [Parameter]
    public bool DialogFullWidth { get; set; } = true;

    [Parameter]
    public bool Mobile { get; set; } = false;

    protected override void OnInitialized()
    {
        var caption = new Caption() { FontSize = "14px" };
        if (Mobile)
        {
            caption = new Caption()
            {
                FontSize = "13px",
            };
        }
        Theme = new MudTheme() { Typography = new Typography() { Caption = caption } };
    }
}
