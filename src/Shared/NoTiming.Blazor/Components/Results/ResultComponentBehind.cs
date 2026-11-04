using Microsoft.AspNetCore.Components;
using NC.Blazor.Components.Abstractions;
using NoTiming.Domain.Core.Objects.Documents;

namespace NoTiming.Blazor.Components.Results;

public class ResultComponentBehind : NComponent
{
    protected string SectionClass =>
        Document?.IsRanked == false ? "results-print-section results-print-page" : "results-print-section";

    [Parameter]
    public ResultsDocument? Document { get; set; }

    [Parameter]
    public string? LeftLogo { get; set; }

    [Parameter]
    public string? RightLogo { get; set; }

    [Parameter]
    public EventCallback<string> LogoClicked { get; set; }
}
