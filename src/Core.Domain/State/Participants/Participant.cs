using Core.Domain.AggregateRoots.Manager;
using Core.Domain.Common.Exceptions;
using Core.Domain.Common.Models;
using Core.Domain.State.Athletes;
using Core.Domain.State.Horses;
using Core.Domain.State.LapRecords;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Core.Domain.State.Participants;

public class Participant : DomainBase<ParticipantException>, IParticipantState
{
    public const int DEFAULT_MAX_AVERAGE_SPEED = 16;
    private const string NAME_FORMAT = "{0} - {1} with {2}";

    private ObservableCollection<LapRecord> lapRecords = new();
    private readonly ReadOnlyObservableCollection<LapRecord> lapRecordsReadonly;

    private Participant()
    {
        this.lapRecordsReadonly = new(this.lapRecords);
    }
    public Participant(Athlete athlete, Horse horse, IParticipantState state = null) : base(GENERATE_ID)
    {
        this.lapRecordsReadonly = new ReadOnlyObservableCollection<LapRecord>(this.lapRecords);
        this.Athlete = athlete;
        this.Horse = horse;
        this.MaxAverageSpeedInKmPh = state?.MaxAverageSpeedInKmPh;
        if (!int.TryParse(state?.Number, out var _))
        {
            throw Helper.Create<ParticipantException>(
                $"Invalid '{nameof(Number)}' - '{state?.Number}'. Please enter a valid number");
        }
        this.Number = state?.Number;
        this.Unranked = state?.Unranked ?? false;
    }

    public List<RfidTag> RfidTags { get; internal set; } = new();
    public bool Unranked { get; internal set; }
    public string Number { get; internal set; }
    public int? MaxAverageSpeedInKmPh { get; internal set; }
    public Horse Horse { get; internal set; }
    public Athlete Athlete { get; internal set; }
    public ObservableCollection<LapRecord> LapRecords
    {
        get => this.lapRecords;
        private set => this.lapRecords = new ObservableCollection<LapRecord>(value.ToList());
    }

    public void Add(LapRecord record)
        => this.lapRecords.Add(record);

    public string Name => FormatName(this.Number, this.Athlete.Name, this.Horse.Name);

    public static string FormatName(string number, string athleteName, string horseName)
    {
        return string.Format(NAME_FORMAT, number, athleteName, horseName);
    }

    public void __REMOVE_RECORDS__()
    {
        this.lapRecords.Clear();
    }

    // Metadata for stats
    public Dictionary<WitnessEventType, List<int>> DetectedHead { get; } = new()
    {
        { WitnessEventType.Arrival, new List<int>() },
        { WitnessEventType.VetIn, new List<int>() },
    };
}
