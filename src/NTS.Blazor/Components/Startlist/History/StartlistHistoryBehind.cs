using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Startlists;

namespace NoTiming.Ui.Components.Startlist.History;

public class StartlistHistoryBehind : NStatefulComponent
{
    [Inject]
    protected IStartHistory Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
