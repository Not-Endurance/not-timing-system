using Microsoft.AspNetCore.Components;
using Not.Blazor.Components;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public partial class StartlistUpcoming
{
    protected override string[] TableHeaders => [.. base.TableHeaders, "Start In"];

    [Inject]
    public IStartlistUpcoming Behind { get; set; } = default!;

    //TEST PARAMETER REMOVE AFTER WITNESS STARTS USING BEHINDS
    [Parameter]
    public List<StartlistEntry> Entries { get; set; } = [];


    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind, []);
        CreateStartlistsByStage(Behind.Upcoming);
        CreateStartlistsByStage(Entries);
    }

    protected override void OnBeforeRender()
    {
        CreateStartlistsByStage(Behind.Upcoming);
        CreateStartlistsByStage(Entries);
    }
}
