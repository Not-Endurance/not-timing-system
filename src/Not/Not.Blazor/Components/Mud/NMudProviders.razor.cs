using MudBlazor;

namespace Not.Blazor.Components.Mud;

    public partial class NMudProviders
    {
        MudThemeProvider _themeProvider = default!;

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

        protected override void OnParametersSet()
        {
            _themeProvider = ThemeProvider;
        }
    }

