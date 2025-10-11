using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Dialogs;
using Not.Exceptions;
using Not.Notify;
using Not.Safe;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using Color = MudBlazor.Color;

namespace NTS.Witness.Components.Pages.Snapshot;

public class SnapshotBehind : NComponent
{
    [Inject]
    IDialogService MudDialogService { get; set; } = null!;

    protected List<Participation> Participations { get; set; }  = [];
    protected List<IntermediateSnapshot> SelectedParticipations { get; set; } = [];
    protected List<IntermediateSnapshot> SnapshotParticipations { get; set; } = [];
    protected string[] SnapshotTableHeaders { get; set; } = [Participant_string, Time_string];
    protected string ButtonText { get; set; } = Arrival_string;

    protected override void OnInitialized()
    {
        for (var i = 0; i < 10; ++i)
        {
            var country = new Country(1000 + i, null, null, null, null);
            var names = new List<string> { $"FirstName{i + 1}", $"LastName{i + 1}" };
            var person = new Person(names.ToArray());

            var athlete = new Athlete(99 + i, person, country, null, $"username{i + 1}");

            var horse = new Horse(100 + i, $"HorseName{i + 1}", null);

            var combination = new Combination(199 + i, i + 1, athlete, horse, null, (40 + i).ToString(), null, null);

            var phase1 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                i > 10 ? 30 : 20,
                15,
                i > 10 ? 60 : 40,
                NTS.Domain.Enums.CompetitionRuleset.Regional,
                false,
                null,
                DateTimeOffset.Now.AddMinutes(23 + i)
            );

            var phase2 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                i > 10 ? 30 : 20,
                20,
                i > 10 ? 60 : 40,
                NTS.Domain.Enums.CompetitionRuleset.Regional,
                true,
                null,
                null
            );

            var phases = new List<NTS.Domain.Core.Aggregates.Participations.Phase> { phase1, phase2 };

            var phaseCollection = new PhaseCollection(phases);

            var competition = new Competition(
                $"CompetitionName{i}",
                Domain.Enums.CompetitionRuleset.Regional,
                Domain.Enums.CompetitionType.Qualification
            );

            var participation = new Participation(
                2001 + i,
                ParticipationCategory.Senior,
                competition,
                combination,
                phaseCollection,
                null
            );

            Participations.Add(participation);
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
            SafeHelper.HandleError(ex);
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
            SafeHelper.HandleError(ex);
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
            SafeHelper.HandleError(ex);
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
            SafeHelper.HandleError(ex);
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
            SafeHelper.HandleError(ex);
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
            SafeHelper.HandleError(ex);
        }
    }
}
