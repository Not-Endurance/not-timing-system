using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Exceptions;
using Not.Notify;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Application.Watcher;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Witness.Features.Sessions;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Snapshots;

public class SnapshotContentBehind : NStatefulComponent
{
    [Inject]
    IDialogService MudDialogService { get; set; } = default!;

    [Inject]
    ISnapshotService SnapshotService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    [Inject]
    IParticipationContext ParticipationContext { get; set; } = default!;

    [Inject]
    IUserSessionService UserSessionService { get; set; } = default!;

    [Inject]
    INtsSocketContext SocketContext { get; set; } = default!;

    protected List<IntermediateSnapshot> SelectedParticipations { get; set; } = [];
    protected List<IntermediateSnapshot> SnapshotParticipations { get; set; } = [];
    protected string[] SnapshotTableHeaders { get; set; } = [Participant_string, Time_string];
    protected string ButtonText { get; set; } = Arrival_string;

    protected override async Task OnInitializedAsync()
    {
        ParticipationContext.Selected = null;
        await Observe(ParticipationContext);
    }

    protected override void OnBeforeRender()
    {
        TrackSelection();
    }

    protected void SetButtonText(int id)
    {
        try
        {
            switch (id)
            {
                case 0:
                    ButtonText = Arrival_string;
                    break;
                case 1:
                    ButtonText = Vetin_string;
                    break;
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void SnapshotHandler(IntermediateSnapshot snapshotParticipant)
    {
        try
        {
            var currentTime = DateTimeOffset.Now;
            var timestamp = new Timestamp(currentTime);
            snapshotParticipant.Timestamp = timestamp;
            SnapshotParticipations.Add(snapshotParticipant);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task SendHandler(string snapshotType)
    {
        try
        {
            if (SnapshotParticipations.Count == 0)
            {
                return;
            }

            var snapshotPayload = new SnapshotPayload(SnapshotParticipations, snapshotType);
            var snapshotModel = SnapshotModel.MapFrom(snapshotPayload);
            await SnapshotService.PublishSnapshotsAsync(snapshotModel);
            await UserSessionService.AppendSnapshot(snapshotPayload, SocketContext.Event?.Id);

            StateHasChanged();
            Notifier.Success(string.Format(Snapshots_sent_as__string, snapshotType));
            SnapshotParticipations.ForEach(p => SelectedParticipations.Remove(p));
            SnapshotParticipations.Clear();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task EditSnapshot(IntermediateSnapshot snapshotParticipant)
    {
        try
        {
            GuardHelper.ThrowIfDefault(snapshotParticipant.Timestamp);
            var time = snapshotParticipant.Timestamp._stamp.TimeOfDay;
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
                snapshotParticipant.Timestamp = new Timestamp(updatedTimestamp.TimestampInput);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    void TrackSelection()
    {
        try
        {
            var participation = ParticipationContext.Selected;
            if (participation == null)
            {
                return;
            }

            AddParticipation(participation);
            ParticipationContext.Selected = null;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    void AddParticipation(Participation participation)
    {
        try
        {
            var snapshotParticipant = new IntermediateSnapshot(
                participation.Combination.Number,
                participation.Combination.Athlete.Names
            );
            if (!SelectedParticipations.Exists(sp => sp.Number == snapshotParticipant.Number))
            {
                SelectedParticipations.Add(snapshotParticipant);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
