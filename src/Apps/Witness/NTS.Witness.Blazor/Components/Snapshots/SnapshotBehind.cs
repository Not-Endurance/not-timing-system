using MudBlazor;
using Not.Blazor.Components;
using Not.Exceptions;
using Not.Notify;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Witness.Services;

namespace NTS.Witness.Blazor.Components.Snapshots;

public class SnapshotBehind : NBehind
{
    [Inject]
    IDialogService MudDialogService { get; set; } = null!;

    [Inject]
    ISnapshotService SnapshotService { get; set; } = null!;

    protected List<Participation> Participations { get; set; } = [];
    protected List<IntermediateSnapshot> SelectedParticipations { get; set; } = [];
    protected List<IntermediateSnapshot> SnapshotParticipations { get; set; } = [];
    protected string[] SnapshotTableHeaders { get; set; } = [Participant_string, Time_string];
    protected string ButtonText { get; set; } = Arrival_string;

    protected override void OnInitialized()
    {
        try
        {
            Participations = SnapshotService.GetParticipations();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
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

    protected void SelectHandler(Participation participation)
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
            StateHasChanged();
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

    protected void SendHandler(string snapshotType)
    {
        try
        {
            //consider backup before clear
            foreach (var participation in SnapshotParticipations)
            {
                NotifyHelper.Inform(
                    $"Participation #{participation.Number} is being sent with Timestamp:{participation.Timestamp}"
                );
            }
            SnapshotParticipations.ForEach(p => SelectedParticipations.Remove(p));
            SnapshotParticipations.Clear();
            NotifyHelper.Success(
                $"Hello and welcome!\nYes, yes, yes! We are doing it.\n I have received word for {snapshotType}!\n Keep up the good work my man! \n Let's gooooooooooo..."
            );
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected Color GetColor(Participation participation)
    {
        try
        {
            if (SnapshotParticipations.Exists(p => p.Number == participation.Combination.Number))
            {
                return Color.Success;
            }
            if (SelectedParticipations.Exists(p => p.Number == participation.Combination.Number))
            {
                return Color.Info;
            }
            return Color.Primary;
        }
        catch (Exception ex)
        {
            Handle(ex);
            return Color.Primary;
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
}
