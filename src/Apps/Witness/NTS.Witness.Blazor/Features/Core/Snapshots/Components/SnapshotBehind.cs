using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor.Features.Core.Snapshots.Components;

public class SnapshotBehind : NComponent
{
    [Parameter]
    public IReadOnlyList<NTS.Domain.Watcher.Snapshot> Snapshots { get; set; } = [];

    [Parameter]
    public string EmptyMessage { get; set; } = string.Empty;

    [Parameter]
    public string[] Headings { get; set; } = [];

    [Parameter]
    public string SnapshotText { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<NTS.Domain.Watcher.Snapshot> OnCapture { get; set; }

    [Parameter]
    public EventCallback<NTS.Domain.Watcher.Snapshot> OnEdit { get; set; }
}
