using Not.Notify;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;
using NTS.Domain.Enums;
using Color = MudBlazor.Color;
using Microsoft.AspNetCore.Components;
using Not.Blazor.Dialogs;

namespace NTS.Witness.Components.Pages.Snapshot;

public partial class Snapshot
{
    List<Participation> _participations = [];
    List<SnapshotParticipant> _selectedParticipations = [];
    List<SnapshotParticipant> _snapshotParticipations = [];
    string[] _snapshotTableHeaders = ["Participant", "Time"];
    string _buttonText = "Arrival";

    [Inject]
    CrudeDialog<SnapshotParticipantUpdateModel, TimestampForm> Dialog { get; set; } = default!;

    protected override void OnInitialized()
    {
        for (var i = 0; i < 2; ++i)
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
                $"username{i+1}"
            );

            var horse = new Horse(100 + i, $"HorseName{i+1}", null);

            var combination = new Combination(
                199 + i,
                i + 1,
                athlete,
                horse,
                null,
                (40 + i).ToString(),
                null,
                null
            );

            var phase1 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                20,
                15,
                40,
                NTS.Domain.Enums.CompetitionRuleset.Regional,
                false,
                null,
                DateTimeOffset.Now.AddMinutes(23 + i)
            );

            var phase2 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                20,
                20,
                40,
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
                competition,
                combination,
                phaseCollection,
                null
            );
           
            _participations.Add(participation);
        }
    }

    void SetButtonText(int id)
    {
        switch (id)
        {
            case 0:
                _buttonText = "Arrival";
                break;
            case 1:
                _buttonText = "VetIn";
                break;
        }
    }

    void SelectHandler(Participation participation)
    {
        var snapshotParticipant = new SnapshotParticipant(participation.Combination.Number, participation.Combination.Athlete.Names);
        if (!_selectedParticipations.Contains(snapshotParticipant))
        {
            _selectedParticipations.Add(snapshotParticipant);
        }
        StateHasChanged();
    }

    void SnapshotHandler(SnapshotParticipant snapshotParticipant)
    {
        var currentTime = DateTimeOffset.Now;
        var timestamp = new Timestamp(currentTime);
        snapshotParticipant.Timestamp = timestamp;
        _snapshotParticipations.Add(snapshotParticipant);
    }

    void SendHandler(string snapshotType)
    {
        //consider backup before clear
        _snapshotParticipations.ForEach(p => _selectedParticipations.Remove(p));
        _snapshotParticipations.Clear();
        NotifyHelper.Success($"Hello and welcome!\nYes, yes, yes! We are doing it.\n I have received word for {snapshotType}!\n Keep up the good work my man! \n Let's gooooooooooo...");
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

    async Task EditSnapshot(SnapshotParticipant snapshotParticipant)
    {
        var model = new SnapshotParticipantUpdateModel(snapshotParticipant);
        await Dialog.RenderUpdate(model);
        await Render();
    }
}

