using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Startlists;

namespace NTS.Blazor.Components.Startlist.History;

public class StartlistHistoryBehind : NStatefulComponent
{
    [Inject]
    protected IStartHistory Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
