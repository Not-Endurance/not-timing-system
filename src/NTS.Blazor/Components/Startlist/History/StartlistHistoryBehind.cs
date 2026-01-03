using Microsoft.AspNetCore.Components;

namespace NTS.Blazor.Components.Startlist.History;

public class StartlistHistoryBehind : StartlistBehindBase
{
    [Inject]
    public IStartHistory Behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind, []);
        CreateStartlistsByStage(Behind.History);
    }

    protected override void OnBeforeRender()
    {
        CreateStartlistsByStage(Behind.History);
    }
}
