using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Blazor.Components.Rankings.Protocols;

public class ProtocolBehind : NComponent
{
    protected bool HasLeftLogo => !string.IsNullOrWhiteSpace(LeftLogo);
    protected bool HasRightLogo => !string.IsNullOrWhiteSpace(RightLogo);
    protected string LogoClass => LogoClicked.HasDelegate ? "cursor-pointer" : string.Empty;

    [Parameter]
    public RanklistDocument? Document { get; set; }

    [Parameter]
    public bool Compact { get; set; }

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
