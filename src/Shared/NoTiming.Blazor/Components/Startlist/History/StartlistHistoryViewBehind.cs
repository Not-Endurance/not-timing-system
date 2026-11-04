using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.History;

public class StartlistHistoryViewBehind : NComponent
{
    protected virtual string[] TableHeaders { get; } = [Number_string, Athlete_string, Loops_string, Start_Time_string];

    [Parameter]
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; set; } =
        new Dictionary<int, IReadOnlyList<Starter>>();

    [Parameter]
    public bool ShowTitle { get; set; } = true;
}
