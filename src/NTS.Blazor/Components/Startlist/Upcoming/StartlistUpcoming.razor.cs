using Microsoft.AspNetCore.Components;
using Not.Blazor.Components;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public partial class StartlistUpcoming
{
    protected override string[] TableHeaders => [.. base.TableHeaders, "Start In"];

    [Parameter]
    public bool Mobile { get; set; } = false;

    [Inject]
    public IStartlistUpcoming Behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind, []);
        CreateStartlistsByStage(Behind.Upcoming);
    }

    protected override void OnBeforeRender()
    {
        CreateStartlistsByStage(Behind.Upcoming);
    }
}
