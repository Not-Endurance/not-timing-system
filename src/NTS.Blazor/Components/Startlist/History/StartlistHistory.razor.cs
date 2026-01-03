using Microsoft.AspNetCore.Components;

namespace NTS.Blazor.Components.Startlist.History;

public partial class StartlistHistory
{
    [Inject]
    public IStartHistory Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service, []);
        CreateStartlistsByStage(Service.History);
    }

    protected override void OnBeforeRender()
    {
        CreateStartlistsByStage(Service.History);
    }
}
