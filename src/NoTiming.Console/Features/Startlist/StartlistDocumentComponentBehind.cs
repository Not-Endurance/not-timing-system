using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Console.Features.Startlist;

/// <summary>The sheet of the Startlist as the page and the printer show it: the header of the Event, and a table for each stage.</summary>
public class StartlistDocumentComponentBehind : NComponent
{
    protected static string[] Headings { get; } = [Number_string, Athlete_string, Loops_string, Start_Time_string];

    [Parameter]
    public StartlistDocument? Document { get; set; }

    [Parameter]
    public string? LeftLogo { get; set; }

    [Parameter]
    public string? RightLogo { get; set; }
}
