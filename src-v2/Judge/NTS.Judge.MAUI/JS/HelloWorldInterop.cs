using BlazorTemplater;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.UI.Xaml.Controls.Primitives;
using MudBlazor;
using MudBlazor.Utilities;
using Not.Blazor.Forms;
using NTS.Judge.Blazor.Pages.Demos;
using NTS.Judge.Ports;
using System.Globalization;

namespace NTS.Judge.MAUI.JS;

public class HelloWorldInterop : IHelloWorldInterop
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IServiceProvider _provider;

    public HelloWorldInterop(IJSRuntime jsRuntime, IServiceProvider provider)
    {
        _jsRuntime = jsRuntime;
        _provider = provider;
    }

    public async Task Hello()
    {
        var files = Directory.GetFiles("./");
        var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/hello-world.js");
        await module.InvokeVoidAsync("hello");
    }

    public async Task Html(MudThemeProvider mudThemeProvider)
    {
        var mudCss = await File.ReadAllTextAsync("./wwwroot/_content/MudBlazor/MudBlazor.min.css");
        var html = new ComponentRenderer<PrintDemo>()
            .AddServiceProvider(_provider)
            .Render();

        var repalcedCss = AddThemedColors(mudCss, mudThemeProvider);

        var pm = new PreMailer.Net.PreMailer(html);
        var result = pm.MoveCssInline(css: repalcedCss);
        ;
    }

    private string AddThemedColors(string css, MudThemeProvider themeProvider)
    {
        var theme = themeProvider.Theme!;
        var palette = themeProvider.IsDarkMode ? theme.PaletteDark : theme.Palette;


        //Palette
        css = css.Replace("var(--mud-palette-black)", $"{palette.Black};");
        css = css.Replace("var(--mud-palette-white)", $"{palette.White};");

        css = css.Replace("var(--mud-palette-primary)", $"{palette.Primary};");
        css = css.Replace("var(--mud-palette-primary-rgb)", $"{palette.Primary.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-primary-text)", $"{palette.PrimaryContrastText};");
        css = css.Replace("var(--mud-palette-primary-darken)", $"{palette.PrimaryDarken};");
        css = css.Replace("var(--mud-palette-primary-lighten)", $"{palette.PrimaryLighten};");
        css = css.Replace("var(--mud-palette-primary-hover)", $"{palette.Primary.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-secondary)", $"{palette.Secondary};");
        css = css.Replace("var(--mud-palette-secondary-rgb)", $"{palette.Secondary.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-secondary-text)", $"{palette.SecondaryContrastText};");
        css = css.Replace("var(--mud-palette-secondary-darken)", $"{palette.SecondaryDarken};");
        css = css.Replace("var(--mud-palette-secondary-lighten)", $"{palette.SecondaryLighten};");
        css = css.Replace("var(--mud-palette-secondary-hover)", $"{palette.Secondary.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-tertiary)", $"{palette.Tertiary};");
        css = css.Replace("var(--mud-palette-tertiary-rgb)", $"{palette.Tertiary.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-tertiary-text)", $"{palette.TertiaryContrastText};");
        css = css.Replace("var(--mud-palette-tertiary-darken)", $"{palette.TertiaryDarken};");
        css = css.Replace("var(--mud-palette-tertiary-lighten)", $"{palette.TertiaryLighten};");
        css = css.Replace("var(--mud-palette-tertiary-hover)", $"{palette.Tertiary.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-info)", $"{palette.Info};");
        css = css.Replace("var(--mud-palette-info-rgb)", $"{palette.Info.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-info-text)", $"{palette.InfoContrastText};");
        css = css.Replace("var(--mud-palette-info-darken)", $"{palette.InfoDarken};");
        css = css.Replace("var(--mud-palette-info-lighten)", $"{palette.InfoLighten};");
        css = css.Replace("var(--mud-palette-info-hover)", $"{palette.Info.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-success)", $"{palette.Success};");
        css = css.Replace("var(--mud-palette-success-rgb)", $"{palette.Success.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-success-text)", $"{palette.SuccessContrastText};");
        css = css.Replace("var(--mud-palette-success-darken)", $"{palette.SuccessDarken};");
        css = css.Replace("var(--mud-palette-success-lighten)", $"{palette.SuccessLighten};");
        css = css.Replace("var(--mud-palette-success-hover)", $"{palette.Success.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-warning)", $"{palette.Warning};");
        css = css.Replace("var(--mud-palette-warning-rgb)", $"{palette.Warning.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-warning-text)", $"{palette.WarningContrastText};");
        css = css.Replace("var(--mud-palette-warning-darken)", $"{palette.WarningDarken};");
        css = css.Replace("var(--mud-palette-warning-lighten)", $"{palette.WarningLighten};");
        css = css.Replace("var(--mud-palette-warning-hover)", $"{palette.Warning.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-error)", $"{palette.Error};");
        css = css.Replace("var(--mud-palette-error-rgb)", $"{palette.Error.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-error-text)", $"{palette.ErrorContrastText};");
        css = css.Replace("var(--mud-palette-error-darken)", $"{palette.ErrorDarken};");
        css = css.Replace("var(--mud-palette-error-lighten)", $"{palette.ErrorLighten};");
        css = css.Replace("var(--mud-palette-error-hover)", $"{palette.Error.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-dark)", $"{palette.Dark};");
        css = css.Replace("var(--mud-palette-dark-rgb)", $"{palette.Dark.ToString(MudColorOutputFormats.ColorElements)};");
        css = css.Replace("var(--mud-palette-dark-text)", $"{palette.DarkContrastText};");
        css = css.Replace("var(--mud-palette-dark-darken)", $"{palette.DarkDarken};");
        css = css.Replace("var(--mud-palette-dark-lighten)", $"{palette.DarkLighten};");
        css = css.Replace("var(--mud-palette-dark-hover)", $"{palette.Dark.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");

        css = css.Replace("var(--mud-palette-text-primary)", $"{palette.TextPrimary};");
        css = css.Replace("var(--mud-palette-text-secondary)", $"{palette.TextSecondary};");
        css = css.Replace("var(--mud-palette-text-disabled)", $"{palette.TextDisabled};");

        css = css.Replace("var(--mud-palette-action-default)", $"{palette.ActionDefault};");
        css = css.Replace("var(--mud-palette-action-default-hover)", $"{palette.ActionDefault.SetAlpha(palette.HoverOpacity).ToString(MudColorOutputFormats.RGBA)};");
        css = css.Replace("var(--mud-palette-action-disabled)", $"{palette.ActionDisabled};");
        css = css.Replace("var(--mud-palette-action-disabled-background)", $"{palette.ActionDisabledBackground};");

        css = css.Replace("var(--mud-palette-surface)", $"{palette.Surface};");
        css = css.Replace("var(--mud-palette-background)", $"{palette.Background};");
        css = css.Replace("var(--mud-palette-background-grey)", $"{palette.BackgroundGrey};");
        css = css.Replace("var(--mud-palette-drawer-background)", $"{palette.DrawerBackground};");
        css = css.Replace("var(--mud-palette-drawer-text)", $"{palette.DrawerText};");
        css = css.Replace("var(--mud-palette-drawer-icon)", $"{palette.DrawerIcon};");
        css = css.Replace("var(--mud-palette-appbar-background)", $"{palette.AppbarBackground};");
        css = css.Replace("var(--mud-palette-appbar-text)", $"{palette.AppbarText};");

        css = css.Replace("var(--mud-palette-lines-default)", $"{palette.LinesDefault};");
        css = css.Replace("var(--mud-palette-lines-inputs)", $"{palette.LinesInputs};");

        css = css.Replace("var(--mud-palette-table-lines)", $"{palette.TableLines};");
        css = css.Replace("var(--mud-palette-table-striped)", $"{palette.TableStriped};");
        css = css.Replace("var(--mud-palette-table-hover)", $"{palette.TableHover};");

        css = css.Replace("var(--mud-palette-divider)", $"{palette.Divider};");
        css = css.Replace("var(--mud-palette-divider-light)", $"{palette.DividerLight};");

        css = css.Replace("var(--mud-palette-gray-default)", $"{palette.GrayDefault};");
        css = css.Replace("var(--mud-palette-gray-light)", $"{palette.GrayLight};");
        css = css.Replace("var(--mud-palette-gray-lighter)", $"{palette.GrayLighter};");
        css = css.Replace("var(--mud-palette-gray-dark)", $"{palette.GrayDark};");
        css = css.Replace("var(--mud-palette-gray-darker)", $"{palette.GrayDarker};");

        css = css.Replace("var(--mud-palette-overlay-dark)", $"{palette.OverlayDark};");
        css = css.Replace("var(--mud-palette-overlay-light)", $"{palette.OverlayLight};");

        //Ripple
        css = css.Replace($"var(--mud-ripple-color)", $"var(--mud-palette-text-primary);");

        //Elevations
        css = css.Replace($"var(--mud-elevation-0)", $"{theme.Shadows.Elevation.GetValue(0)};");
        css = css.Replace($"var(--mud-elevation-1)", $"{theme.Shadows.Elevation.GetValue(1)};");
        css = css.Replace($"var(--mud-elevation-2)", $"{theme.Shadows.Elevation.GetValue(2)};");
        css = css.Replace($"var(--mud-elevation-3)", $"{theme.Shadows.Elevation.GetValue(3)};");
        css = css.Replace($"var(--mud-elevation-4)", $"{theme.Shadows.Elevation.GetValue(4)};");
        css = css.Replace($"var(--mud-elevation-5)", $"{theme.Shadows.Elevation.GetValue(5)};");
        css = css.Replace($"var(--mud-elevation-6)", $"{theme.Shadows.Elevation.GetValue(6)};");
        css = css.Replace($"var(--mud-elevation-7)", $"{theme.Shadows.Elevation.GetValue(7)};");
        css = css.Replace($"var(--mud-elevation-8)", $"{theme.Shadows.Elevation.GetValue(8)};");
        css = css.Replace($"var(--mud-elevation-9)", $"{theme.Shadows.Elevation.GetValue(9)};");
        css = css.Replace($"var(--mud-elevation-10)", $"{theme.Shadows.Elevation.GetValue(10)};");
        css = css.Replace($"var(--mud-elevation-11)", $"{theme.Shadows.Elevation.GetValue(11)};");
        css = css.Replace($"var(--mud-elevation-12)", $"{theme.Shadows.Elevation.GetValue(12)};");
        css = css.Replace($"var(--mud-elevation-13)", $"{theme.Shadows.Elevation.GetValue(13)};");
        css = css.Replace($"var(--mud-elevation-14)", $"{theme.Shadows.Elevation.GetValue(14)};");
        css = css.Replace($"var(--mud-elevation-15)", $"{theme.Shadows.Elevation.GetValue(15)};");
        css = css.Replace($"var(--mud-elevation-16)", $"{theme.Shadows.Elevation.GetValue(16)};");
        css = css.Replace($"var(--mud-elevation-17)", $"{theme.Shadows.Elevation.GetValue(17)};");
        css = css.Replace($"var(--mud-elevation-18)", $"{theme.Shadows.Elevation.GetValue(18)};");
        css = css.Replace($"var(--mud-elevation-19)", $"{theme.Shadows.Elevation.GetValue(19)};");
        css = css.Replace($"var(--mud-elevation-20)", $"{theme.Shadows.Elevation.GetValue(20)};");
        css = css.Replace($"var(--mud-elevation-21)", $"{theme.Shadows.Elevation.GetValue(21)};");
        css = css.Replace($"var(--mud-elevation-22)", $"{theme.Shadows.Elevation.GetValue(22)};");
        css = css.Replace($"var(--mud-elevation-23)", $"{theme.Shadows.Elevation.GetValue(23)};");
        css = css.Replace($"var(--mud-elevation-24)", $"{theme.Shadows.Elevation.GetValue(24)};");
        css = css.Replace($"var(--mud-elevation-25)", $"{theme.Shadows.Elevation.GetValue(25)};");

        //Layout Properties
        css = css.Replace("var(--mud-default-borderradius)", $"{theme.LayoutProperties.DefaultBorderRadius};");
        css = css.Replace("var(--mud-drawer-width-left)", $"{theme.LayoutProperties.DrawerWidthLeft};");
        css = css.Replace("var(--mud-drawer-width-right)", $"{theme.LayoutProperties.DrawerWidthRight};");
        css = css.Replace("var(--mud-drawer-width-mini-left)", $"{theme.LayoutProperties.DrawerMiniWidthLeft};");
        css = css.Replace("var(--mud-drawer-width-mini-right)", $"{theme.LayoutProperties.DrawerMiniWidthRight};");
        css = css.Replace("var(--mud-appbar-height)", $"{theme.LayoutProperties.AppbarHeight};");

        //Typography
        css = css.Replace("var(--mud-typography-default-family)", $"'{string.Join("','", theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-default-size)", $"{theme.Typography.Default.FontSize};");
        css = css.Replace("var(--mud-typography-default-weight)", $"{theme.Typography.Default.FontWeight};");
        css = css.Replace("var(--mud-typography-default-lineheight)", $"{theme.Typography.Default.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-default-letterspacing)", $"{theme.Typography.Default.LetterSpacing};");
        css = css.Replace("var(--mud-typography-default-text-transform)", $"{theme.Typography.Default.TextTransform};");

        css = css.Replace("var(--mud-typography-h1-family)", $"'{string.Join("','", theme.Typography.H1.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-h1-size)", $"{theme.Typography.H1.FontSize};");
        css = css.Replace("var(--mud-typography-h1-weight)", $"{theme.Typography.H1.FontWeight};");
        css = css.Replace("var(--mud-typography-h1-lineheight)", $"{theme.Typography.H1.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-h1-letterspacing)", $"{theme.Typography.H1.LetterSpacing};");
        css = css.Replace("var(--mud-typography-h1-text-transform)", $"{theme.Typography.H1.TextTransform};");

        css = css.Replace("var(--mud-typography-h2-family)", $"'{string.Join("','", theme.Typography.H2.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-h2-size)", $"{theme.Typography.H2.FontSize};");
        css = css.Replace("var(--mud-typography-h2-weight)", $"{theme.Typography.H2.FontWeight};");
        css = css.Replace("var(--mud-typography-h2-lineheight)", $"{theme.Typography.H2.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-h2-letterspacing)", $"{theme.Typography.H2.LetterSpacing};");
        css = css.Replace("var(--mud-typography-h2-text-transform)", $"{theme.Typography.H2.TextTransform};");

        css = css.Replace("var(--mud-typography-h3-family)", $"'{string.Join("','", theme.Typography.H3.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-h3-size)", $"{theme.Typography.H3.FontSize};");
        css = css.Replace("var(--mud-typography-h3-weight)", $"{theme.Typography.H3.FontWeight};");
        css = css.Replace("var(--mud-typography-h3-lineheight)", $"{theme.Typography.H3.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-h3-letterspacing)", $"{theme.Typography.H3.LetterSpacing};");
        css = css.Replace("var(--mud-typography-h3-text-transform)", $"{theme.Typography.H3.TextTransform};");

        css = css.Replace("var(--mud-typography-h4-family)", $"'{string.Join("','", theme.Typography.H4.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-h4-size)", $"{theme.Typography.H4.FontSize};");
        css = css.Replace("var(--mud-typography-h4-weight)", $"{theme.Typography.H4.FontWeight};");
        css = css.Replace("var(--mud-typography-h4-lineheight)", $"{theme.Typography.H4.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-h4-letterspacing)", $"{theme.Typography.H4.LetterSpacing};");
        css = css.Replace("var(--mud-typography-h4-text-transform)", $"{theme.Typography.H4.TextTransform};");

        css = css.Replace("var(--mud-typography-h5-family)", $"'{string.Join("','", theme.Typography.H5.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-h5-size)", $"{theme.Typography.H5.FontSize};");
        css = css.Replace("var(--mud-typography-h5-weight)", $"{theme.Typography.H5.FontWeight};");
        css = css.Replace("var(--mud-typography-h5-lineheight)", $"{theme.Typography.H5.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-h5-letterspacing)", $"{theme.Typography.H5.LetterSpacing};");
        css = css.Replace("var(--mud-typography-h5-text-transform)", $"{theme.Typography.H5.TextTransform};");

        css = css.Replace("var(--mud-typography-h6-family)", $"'{string.Join("','", theme.Typography.H6.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-h6-size)", $"{theme.Typography.H6.FontSize};");
        css = css.Replace("var(--mud-typography-h6-weight)", $"{theme.Typography.H6.FontWeight};");
        css = css.Replace("var(--mud-typography-h6-lineheight)", $"{theme.Typography.H6.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-h6-letterspacing)", $"{theme.Typography.H6.LetterSpacing};");
        css = css.Replace("var(--mud-typography-h6-text-transform)", $"{theme.Typography.H6.TextTransform};");

        css = css.Replace("var(--mud-typography-subtitle1-family)", $"'{string.Join("','", theme.Typography.Subtitle1.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-subtitle1-size)", $"{theme.Typography.Subtitle1.FontSize};");
        css = css.Replace("var(--mud-typography-subtitle1-weight)", $"{theme.Typography.Subtitle1.FontWeight};");
        css = css.Replace("var(--mud-typography-subtitle1-lineheight)", $"{theme.Typography.Subtitle1.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-subtitle1-letterspacing)", $"{theme.Typography.Subtitle1.LetterSpacing};");
        css = css.Replace("var(--mud-typography-subtitle1-text-transform)", $"{theme.Typography.Subtitle1.TextTransform};");

        css = css.Replace("var(--mud-typography-subtitle2-family)", $"'{string.Join("','", theme.Typography.Subtitle2.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-subtitle2-size)", $"{theme.Typography.Subtitle2.FontSize};");
        css = css.Replace("var(--mud-typography-subtitle2-weight)", $"{theme.Typography.Subtitle2.FontWeight};");
        css = css.Replace("var(--mud-typography-subtitle2-lineheight)", $"{theme.Typography.Subtitle2.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-subtitle2-letterspacing)", $"{theme.Typography.Subtitle2.LetterSpacing};");
        css = css.Replace("var(--mud-typography-subtitle2-text-transform)", $"{theme.Typography.Subtitle2.TextTransform};");

        css = css.Replace("var(--mud-typography-body1-family)", $"'{string.Join("','", theme.Typography.Body1.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-body1-size)", $"{theme.Typography.Body1.FontSize};");
        css = css.Replace("var(--mud-typography-body1-weight)", $"{theme.Typography.Body1.FontWeight};");
        css = css.Replace("var(--mud-typography-body1-lineheight)", $"{theme.Typography.Body1.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-body1-letterspacing)", $"{theme.Typography.Body1.LetterSpacing};");
        css = css.Replace("var(--mud-typography-body1-text-transform)", $"{theme.Typography.Body1.TextTransform};");

        css = css.Replace("var(--mud-typography-body2-family)", $"'{string.Join("','", theme.Typography.Body2.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-body2-size)", $"{theme.Typography.Body2.FontSize};");
        css = css.Replace("var(--mud-typography-body2-weight)", $"{theme.Typography.Body2.FontWeight};");
        css = css.Replace("var(--mud-typography-body2-lineheight)", $"{theme.Typography.Body2.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-body2-letterspacing)", $"{theme.Typography.Body2.LetterSpacing};");
        css = css.Replace("var(--mud-typography-body2-text-transform)", $"{theme.Typography.Body2.TextTransform};");

        css = css.Replace("var(--mud-typography-input-family)", $"'{string.Join("','", theme.Typography.Default.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-input-size)", $"{theme.Typography.Default.FontSize};");
        css = css.Replace("var(--mud-typography-input-weight)", $"{theme.Typography.Default.FontWeight};");
        css = css.Replace("var(--mud-typography-input-lineheight)", $"{theme.Typography.Default.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-input-letterspacing)", $"{theme.Typography.Default.LetterSpacing};");
        css = css.Replace("var(--mud-typography-input-text-transform)", $"{theme.Typography.Default.TextTransform};");

        css = css.Replace("var(--mud-typography-button-family)", $"'{string.Join("','", theme.Typography.Button.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-button-size)", $"{theme.Typography.Button.FontSize};");
        css = css.Replace("var(--mud-typography-button-weight)", $"{theme.Typography.Button.FontWeight};");
        css = css.Replace("var(--mud-typography-button-lineheight)", $"{theme.Typography.Button.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-button-letterspacing)", $"{theme.Typography.Button.LetterSpacing};");
        css = css.Replace("var(--mud-typography-button-text-transform)", $"{theme.Typography.Button.TextTransform};");

        css = css.Replace("var(--mud-typography-caption-family)", $"'{string.Join("','", theme.Typography.Caption.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-caption-size)", $"{theme.Typography.Caption.FontSize};");
        css = css.Replace("var(--mud-typography-caption-weight)", $"{theme.Typography.Caption.FontWeight};");
        css = css.Replace("var(--mud-typography-caption-lineheight)", $"{theme.Typography.Caption.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-caption-letterspacing)", $"{theme.Typography.Caption.LetterSpacing};");
        css = css.Replace("var(--mud-typography-caption-text-transform)", $"{theme.Typography.Caption.TextTransform};");

        css = css.Replace("var(--mud-typography-overline-family)", $"'{string.Join("','", theme.Typography.Overline.FontFamily ?? theme.Typography.Default.FontFamily ?? Array.Empty<string>())}';");
        css = css.Replace("var(--mud-typography-overline-size)", $"{theme.Typography.Overline.FontSize};");
        css = css.Replace("var(--mud-typography-overline-weight)", $"{theme.Typography.Overline.FontWeight};");
        css = css.Replace("var(--mud-typography-overline-lineheight)", $"{theme.Typography.Overline.LineHeight.ToString(CultureInfo.InvariantCulture)};");
        css = css.Replace("var(--mud-typography-overline-letterspacing)", $"{theme.Typography.Overline.LetterSpacing};");
        css = css.Replace("var(--mud-typography-overline-text-transform)", $"{theme.Typography.Overline.TextTransform};");

        //Z-Index
        css = css.Replace("var(--mud-zindex-drawer)", $"{theme.ZIndex.Drawer};");
        css = css.Replace("var(--mud-zindex-appbar)", $"{theme.ZIndex.AppBar};");
        css = css.Replace("var(--mud-zindex-dialog)", $"{theme.ZIndex.Dialog};");
        css = css.Replace("var(--mud-zindex-popover)", $"{theme.ZIndex.Popover};");
        css = css.Replace("var(--mud-zindex-snackbar)", $"{theme.ZIndex.Snackbar};");
        css = css.Replace("var(--mud-zindex-tooltip)", $"{theme.ZIndex.Tooltip};");

        return css;
    }
}
