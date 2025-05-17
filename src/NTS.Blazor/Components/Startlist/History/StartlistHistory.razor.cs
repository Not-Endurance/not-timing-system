using Microsoft.AspNetCore.Components;

namespace NTS.Blazor.Components.Startlist.History;

public partial class StartlistHistory
{
    [Inject]
    public IStartlistHistory Behind { get; set; } = default!;

    [Parameter]
    public bool Mobile { get; set; } = false;

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
