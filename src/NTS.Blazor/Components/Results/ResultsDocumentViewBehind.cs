using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Blazor.Components.Results;

public class ResultsDocumentViewBehind : NComponent
{
    [Parameter]
    public ResultsDocument? Document { get; set; }

    [Parameter]
    public string? LeftLogo { get; set; }

    [Parameter]
    public string? RightLogo { get; set; }

    [Parameter]
    public EventCallback<string> LogoClicked { get; set; }
}
