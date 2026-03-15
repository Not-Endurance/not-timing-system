using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Blazor.Features.Core.Snapshots.Components;

public class SnapshotChipsBehind : NComponent
{
    [Parameter]
    public IReadOnlyList<Participation> Participations { get; set; } = [];

    [Parameter]
    public EventCallback<Participation?> SelectedChanged { get; set; }
}
