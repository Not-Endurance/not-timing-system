using Microsoft.AspNetCore.Components;
using NTS.Application.Startlists;

namespace NTS.Blazor.Components.Startlist.History;

public class StartlistHistoryBehind : StartlistBehindBase
{
    [Inject]
    protected IStartHistory Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }
}
