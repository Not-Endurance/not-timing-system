using Microsoft.AspNetCore.Components;

namespace NTS.Blazor.Components.Startlist.History;

public class StartlistHistoryBehind : StartlistBehindBase
{
    [Inject]
    protected IStartHistory Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected override void OnBeforeRender()
    {
        CreateStartlistsByStage(Service.History);
    }
}
