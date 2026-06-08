using Microsoft.AspNetCore.Components;
using NTS.Application.Contracts;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Blazor.Components.Print;

public class ResultsPrintDocumentBehind : ComponentBase
{
    protected bool UsePagedLayout => Documents.Any(x => !x.IsRanked);
    protected string BackdropLogo => PrintLogoPath.Nts;
    protected string GeneratedByText => $"{Generated_by_NoTiming_System_v_string}{ApplicationConstants.VERSION}";

    [Parameter, EditorRequired]
    public IReadOnlyList<ResultsDocument> Documents { get; set; } = [];

    [Parameter]
    public string? LeftLogo { get; set; }

    [Parameter]
    public string? RightLogo { get; set; }

    [Parameter]
    public EventCallback<string> LogoClicked { get; set; }

    protected string GetSectionClass()
    {
        return UsePagedLayout ? "results-print-section results-print-page" : "results-print-section";
    }
}
