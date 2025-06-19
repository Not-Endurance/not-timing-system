using MudBlazor;
using Not.Blazor.Dialogs;
using Not.Exceptions;
using Not.Notify;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using Color = MudBlazor.Color;

namespace NTS.Witness.Components.Pages.Snapshot;

public partial class Snapshot
{
    List<Participation> _participations = [];
    List<IntermediateSnapshot> _selectedParticipations = [];
    List<IntermediateSnapshot> _snapshotParticipations = [];
    string[] _snapshotTableHeaders = [Participant_string, Time_string];
    string _buttonText = Arrival_string;

    [Inject]
    IDialogService MudDialogService { get; set; } = null!;

    protected override void OnInitialized()
    {
        for (var i = 0; i < 10; ++i)
        {
            var country = new Country(1000 + i, null, null, null, null);
            var names = new List<string> { $"FirstName{i + 1}", $"LastName{i + 1}" };
            var person = new Person(names.ToArray());

            var athlete = new Athlete(
                99 + i,
                person,
                Domain.Enums.AthleteCategory.Senior,
                country,
                null,
                $"username{i + 1}"
            );

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

            var participation = new Participation(2001 + i, competition, combination, phaseCollection, null);

            _participations.Add(participation);
        }
    }

    void SetButtonText(int id)
    {
        switch (id)
        {
            case 0:
                _buttonText = Arrival_string;
                break;
            case 1:
                _buttonText = Vetin_string;
                break;
        }
    }

    void SelectHandler(Participation participation)
    {
        var snapshotParticipant = new IntermediateSnapshot(
            participation.Combination.Number,
            participation.Combination.Athlete.Names
        );
        if (!_selectedParticipations.Exists(sp => sp.Number == snapshotParticipant.Number))
        {
            _selectedParticipations.Add(snapshotParticipant);
        }
        StateHasChanged();
    }

    void SnapshotHandler(IntermediateSnapshot snapshotParticipant)
    {
        var currentTime = DateTimeOffset.Now;
        var timestamp = new Timestamp(currentTime);
        snapshotParticipant.Timestamp = timestamp;
        _snapshotParticipations.Add(snapshotParticipant);
    }

    void SendHandler(string snapshotType)
    {
        //consider backup before clear
        foreach (var participation in _snapshotParticipations)
        {
            NotifyHelper.Inform(
                $"Participation #{participation.Number} is being sent with Timestamp:{participation.Timestamp}"
            );
        }
        _snapshotParticipations.ForEach(p => _selectedParticipations.Remove(p));
        _snapshotParticipations.Clear();
        NotifyHelper.Success(
            $"Hello and welcome!\nYes, yes, yes! We are doing it.\n I have received word for {snapshotType}!\n Keep up the good work my man! \n Let's gooooooooooo..."
        );
    }

    Color GetColor(Participation participation)
    {
        if (_snapshotParticipations.Exists(p => p.Number == participation.Combination.Number))
        {
            return Color.Success;
        }
        if (_selectedParticipations.Exists(p => p.Number == participation.Combination.Number))
        {
            return Color.Info;
        }
        return Color.Primary;
    }

    async Task EditSnapshot(IntermediateSnapshot snapshotParticipant)
    {
        GuardHelper.ThrowIfDefault(snapshotParticipant.Timestamp);
        var time = snapshotParticipant.Timestamp._stamp.TimeOfDay;
        var model = new TimestampUpdateModel(time);
        var parameters = new DialogParameters<TimestampUpdateDialog> { { x => x.Model, model } };
        var options = new DialogOptions() { Position = DialogPosition.Center };
        var dialog = await MudDialogService.ShowAsync<TimestampUpdateDialog>(Edit_timestamp_string, parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            var updatedTimestamp = result.Data as TimestampUpdateModel;
            GuardHelper.ThrowIfDefault(updatedTimestamp);
            GuardHelper.ThrowIfDefault(updatedTimestamp.TimestampInput);
            snapshotParticipant.Timestamp = new Timestamp(updatedTimestamp.TimestampInput);
        }
    }
}
