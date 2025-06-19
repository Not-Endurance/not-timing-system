using MudBlazor;

namespace Not.Blazor.Components.Mud;

public partial class NTheme
{
    MudThemeProvider _themeProvider = default!;
    MudTheme _theme = default!;

    [Parameter]
    public MudThemeProvider ThemeProvider { get; set; } = default!;

    [Parameter]
    public MudTheme? Theme { get; set; }

    [Parameter]
    public bool DialogCloseOnEscapeKey { get; set; } = true;

    [Parameter]
    public bool DialogCloseButton { get; set; } = true;

    [Parameter]
    public MaxWidth DialogMaxWidth { get; set; } = MaxWidth.Small;

    [Parameter]
    public bool DialogFullWidth { get; set; } = true;

    protected override void OnParametersSet()
    {
        _themeProvider = ThemeProvider;
        _theme = new MudTheme()
        {
            Typography = new Typography()
            {
                Caption = new Caption()
                {
                    FontFamily = new[] { "Roboto", "Helvetica", "Arial", "sans-serif" },
                    FontSize = "12px",
                    FontWeight = 400,
                    LineHeight = 1.0,
                    LetterSpacing = ".0075em",
                },
            },
        };
    }
}
