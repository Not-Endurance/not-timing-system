using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Snapshots;

namespace NoTiming.Ui.Features.Core.Snapshots.Components;

/// <summary>
/// Says that Snapshots are waiting to be sent (#645): the group that was sent and no answer came to is the device's, and
/// the app sends it again until the server answers. The page of an Event asks for the sending to go on when it opens, as the
/// group may have been kept by a page that was closed.
/// </summary>
public class SnapshotUnansweredBehind : NStatefulComponent
{
    [Inject]
    ISnapshotService SnapshotService { get; set; } = default!;

    [CascadingParameter]
    IViewedEvent View { get; set; } = default!;

    protected int Count => SnapshotService.Unanswered?.Entries.Count() ?? 0;

    protected override async Task OnInitializedAsync()
    {
        await Observe(SnapshotService);
        SnapshotService.Resume(View);
    }
}
