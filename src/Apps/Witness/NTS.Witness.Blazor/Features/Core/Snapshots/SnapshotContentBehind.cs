using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Exceptions;
using Not.Notify;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Witness.Blazor.Features.Core.Snapshots.SnapshotUpdate;
using NTS.Witness.Blazor.Features.Socket;
using NTS.Witness.Features.Access;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Snapshots;

public class SnapshotContentBehind : NStatefulComponent
{
    [Inject]
    IDialogService MudDialogService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    [Inject]
    ISnapshotService SnapshotState { get; set; } = default!;

    [Inject]
    BlazorSocketService BlazorSocketService { get; set; } = default!;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected ISnapshotService SnapshotService => SnapshotState;
    protected IReadOnlyList<Participation> Participations => SnapshotService.Participations;
    protected IReadOnlyList<Snapshot> Snapshots => SnapshotService.Snapshots;
    protected string[] SnapshotTableHeaders { get; set; } = [Participant_string, Time_string];
    protected SnapshotType SelectedSnapshotType { get; set; } = SnapshotType.Arrive;
    protected int ReadySnapshotCount => Snapshots.Count(x => x.Timestamp != null);
    protected bool HasReadySnapshots => ReadySnapshotCount != 0;
    protected bool IsPublishing { get; set; }
    protected string PublishButtonText => GetSnapshotTypeText(SelectedSnapshotType);

    protected override async Task OnInitializedAsync()
    {
        await Observe(AccessState);
        await Observe(SnapshotService);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (WitnessAccessPolicy.ShouldRedirectFromSnapshots(AccessState.AccessLevel))
        {
            Navigator.NavigateTo(WitnessAccessPolicy.ResolveSnapshotFallbackRoute());
            return;
        }

        if (firstRender)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await BlazorSocketService.EnsureConnected();
        }
    }

    protected void SetSnapshotType(SnapshotType snapshotType)
    {
        try
        {
            SelectedSnapshotType = snapshotType;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void SnapshotHandler(Snapshot snapshot)
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

    protected async Task SendHandler(SnapshotType snapshotType)
    {
        try
        {
            if (IsPublishing)
            {
                return;
            }

            IsPublishing = true;
            if (!await SnapshotService.Publish(snapshotType))
            {
                return;
            }

            Notifier.Success(string.Format(Snapshots_sent_as__string, GetSnapshotTypeText(snapshotType)));
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            IsPublishing = false;
            StateHasChanged();
        }
    }

    protected async Task EditSnapshot(Snapshot snapshot)
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
                SnapshotService.UpdateSnapshotTimestamp(snapshot, new Timestamp(updatedTimestamp.TimestampInput));
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected Task MoveToSnapshot(Participation? participation)
    {
        try
        {
            if (participation != null)
            {
                SnapshotService.MoveToSnapshot(participation);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }

        return Task.CompletedTask;
    }

    protected string GetSnapshotTypeText(SnapshotType snapshotType)
    {
        return snapshotType switch
        {
            SnapshotType.Arrive => Arrive_string,
            SnapshotType.Present => Presentation_string,
            _ => snapshotType.ToString(),
        };
    }
}
