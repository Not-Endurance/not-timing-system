using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Dialogs;
using Not.Blazor.Helpers;
using Not.Exceptions;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Witness.Blazor.Features.Core.Snapshots.SnapshotUpdate;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Snapshots.Components;

public class SnapshotComponentBehind : NComponent
{
    [Inject]
    ISnapshotService SnapshotService { get; set; } = default!;

    [Inject]
    IDialogService MudDialogService { get; set; } = default!;

    protected IEnumerable<Snapshot> OrderedSnapshots => Snapshots.OrderBy(x => x.Timestamp != null);

    [Parameter]
    public bool Compact { get; set; }

    [Parameter]
    public IReadOnlyList<Snapshot> Snapshots { get; set; } = [];

    protected string GetTableClass()
    {
        return Compact
            ? "snapshot-table snapshot-table-compact"
            : "snapshot-table";
    }

    protected string GetButtonStyle()
    {
        return Compact ? "width:90px" : "width:100px";
    }

    protected void CaptureSnapshot(Snapshot snapshot)
    {
        try
        {
            SnapshotService.CaptureSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ShowEditSnapshotDialog(Snapshot snapshot)
    {
        try
        {
            GuardHelper.ThrowIfDefault(snapshot.Timestamp);
            var time = snapshot.Timestamp._stamp.TimeOfDay;
            var model = new TimestampUpdateModel(time);
            var parameters = new DialogParameters<TimestampUpdateDialog> { { x => x.Model, model } };
            var options = new DialogOptions() { Position = DialogPosition.Center };
            var dialog = await MudDialogService.ShowAsync<TimestampUpdateDialog>(
                Edit_timestamp_string,
                parameters,
                options
            );
            var result = await dialog.Result;

            if (result != null && !result.Canceled)
            {
                var updatedTimestamp = result.Data as TimestampUpdateModel;
                GuardHelper.ThrowIfDefault(updatedTimestamp);
                GuardHelper.ThrowIfDefault(updatedTimestamp.TimestampInput);
                var stamp = new Timestamp(updatedTimestamp.TimestampInput);
                SnapshotService.UpdateSnapshotTimestamp(snapshot, stamp);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task RemoveSnapshotOrShowDialog(Snapshot snapshot)
    {
        try
        {
            if (snapshot.Timestamp != null)
            {
                var parameters = new DialogParameters<NConfirmationDialog>
                {
                    {
                        x => x.Description,
                        string.Format(Are_you_sure_you_want_to_remove__the_snapshot_will_be_lost_string, snapshot)
                    },
                };
                var dialog = await MudDialogService.ShowAsync<NConfirmationDialog>(Confirm_action_string, parameters);
                if (await dialog.IsCanceled())
                {
                    return;
                }
            }

            SnapshotService.RemoveSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
