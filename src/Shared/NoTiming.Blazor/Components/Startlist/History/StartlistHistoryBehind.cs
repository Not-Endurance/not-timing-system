using Microsoft.AspNetCore.Components;
using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Startlists;

namespace NoTiming.Blazor.Components.Startlist.History;

public class StartlistHistoryBehind : NStatefulComponent
{
    [Inject]
    protected IStartHistory Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
