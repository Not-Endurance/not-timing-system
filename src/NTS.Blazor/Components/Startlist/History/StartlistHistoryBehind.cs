using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Startlists;

namespace NTS.Blazor.Components.Startlist.History;

public class StartlistHistoryBehind : NStatefulComponent
{
    protected virtual string[] TableHeaders { get; } = [Number_string, Athlete_string, Loops_string, Start_Time_string];

    [Inject]
    protected IStartHistory Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
