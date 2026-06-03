using Microsoft.AspNetCore.Components;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Blazor.Components.Print;

public class RanklistPrintDocumentBehind : ComponentBase
{
    [Parameter]
    public ProtocolDocument? Document { get; set; }

    [Parameter]
    public bool Compact { get; set; } = true;

    [Parameter]
    public bool PhasesAsRows { get; set; }

    [Parameter]
    public bool ShowRanks { get; set; } = true;

    [Parameter]
    public string? LeftLogo { get; set; }

    [Parameter]
    public string? RightLogo { get; set; }

    [Parameter]
    public EventCallback<string> LogoClicked { get; set; }
}
