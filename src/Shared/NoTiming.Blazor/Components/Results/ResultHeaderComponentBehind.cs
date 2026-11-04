using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Blazor.Components.Results;

public class ResultHeaderComponentBehind : NComponent
{
    protected bool HasLeftLogo => !string.IsNullOrWhiteSpace(LeftLogo);
    protected bool HasRightLogo => !string.IsNullOrWhiteSpace(RightLogo);
    protected string LogoClass => LogoClicked.HasDelegate ? "cursor-pointer" : string.Empty;
    protected string HeaderLogoClass =>
        string.IsNullOrWhiteSpace(LogoClass) ? "results-header-logo" : $"results-header-logo {LogoClass}";

    [Parameter, EditorRequired]
    public DocumentHeader Header { get; set; } = default!;

    [Parameter]
    public string? LeftLogo { get; set; }

    [Parameter]
    public string? RightLogo { get; set; }

    [Parameter]
    public EventCallback<string> LogoClicked { get; set; }

    protected async Task OnLogoClicked(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo) || !LogoClicked.HasDelegate)
        {
            return;
        }

        await LogoClicked.InvokeAsync(logo);
    }
}
