using Microsoft.AspNetCore.Components;
using NC.Blazor.Components.Abstractions;
using NoTiming.Domain.Core.Objects.Startlists;

namespace NoTiming.Blazor.Components.Startlist.History;

public class StartlistHistoryViewBehind : NComponent
{
    protected virtual string[] TableHeaders { get; } = [Number_string, Athlete_string, Loops_string, Start_Time_string];

    [Parameter]
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage { get; set; } =
        new Dictionary<int, IReadOnlyList<Starter>>();

    [Parameter]
    public bool ShowTitle { get; set; } = true;
}
