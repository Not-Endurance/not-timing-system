using NoTiming.Ui.Features.Core.EventViews;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Snapshots;

namespace NoTiming.Ui.Features.Core.Snapshots;

public class SnapshotContentBehind : EventPageBehind
{
    [Inject]
    ISnapshotService SnapshotState { get; set; } = default!;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    protected ISnapshotService SnapshotService => SnapshotState;
    protected IReadOnlyList<Participation> Participations => SnapshotService.Participations;
    protected IReadOnlyList<Snapshot> Snapshots => SnapshotService.Snapshots;

    protected override async Task OnInitializedAsync()
    {
        await Observe(AccessState);
        await Observe(SnapshotService);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (EventId == null)
            {
                await BlazorSocketService.EnsureConnected();
            }
        }
    }

    protected Task MoveToSnapshot(Participation? participation)
    {
        try
        {
            if (participation != null)
            {
                SnapshotService.SelectForSnapshot(participation);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }

        return Task.CompletedTask;
    }
}
