using System.Collections.ObjectModel;
using Newtonsoft.Json;
using NTS.Relay.ACL.Abstractions;
using NTS.Relay.ACL.Entities.Athletes;
using NTS.Relay.ACL.Entities.Horses;
using NTS.Relay.ACL.Entities.LapRecords;

namespace NTS.Relay.ACL.Entities.Participants;

public class EmsParticipant : EmsDomainBase<EmsParticipantException>, IEmsParticipantState
{
    const string NAME_FORMAT = "{0} - {1} with {2}";
    public const int DEFAULT_MAX_AVERAGE_SPEED = 16;

    public static string FormatName(string number, string athleteName, string horseName)
    {
        return string.Format(NAME_FORMAT, number, athleteName, horseName);
    }

#pragma warning disable IDE0052 // Remove unread private members
    readonly ReadOnlyObservableCollection<EmsLapRecord> lapRecordsReadonly;
#pragma warning restore IDE0052 // Remove unread private members
    ObservableCollection<EmsLapRecord> lapRecords = [];

    [JsonConstructor]
    EmsParticipant()
    {
        lapRecordsReadonly = new(this.lapRecords);
    }

    public EmsParticipant(EmsAthlete athlete, EmsHorse horse, IEmsParticipantState state = null)
        : base(GENERATE_ID)
    {
        lapRecordsReadonly = new ReadOnlyObservableCollection<EmsLapRecord>(this.lapRecords);
        Athlete = athlete;
        Horse = horse;
        MaxAverageSpeedInKmPh = state?.MaxAverageSpeedInKmPh;
        if (!int.TryParse(state?.Number, out var _))
        {
            throw EmsHelper.Create<EmsParticipantException>(
                $"Invalid '{nameof(Number)}' - '{state?.Number}'. Please enter a valid number"
            );
        }
        Number = state?.Number;
        Unranked = state?.Unranked ?? false;
    }

    public List<EmsRfidTag> RfidTags { get; internal set; } = [];
    public bool Unranked { get; internal set; }
    public string Number { get; internal set; }
    public int? MaxAverageSpeedInKmPh { get; internal set; }
    public EmsHorse Horse { get; internal set; }
    public EmsAthlete Athlete { get; internal set; }
    public ObservableCollection<EmsLapRecord> LapRecords
    {
        get => lapRecords;
        private set => lapRecords = new ObservableCollection<EmsLapRecord>(value.ToList());
    }
    public string Name => FormatName(this.Number, Athlete.Name, Horse.Name);
    public Dictionary<WitnessEventType, List<int>> DetectedHead { get; } = // Metadata for stats
        new() { { WitnessEventType.Arrival, new List<int>() }, { WitnessEventType.VetIn, new List<int>() } };

    public void Add(EmsLapRecord record)
    {
        lapRecords.Add(record);
    }

    public void __REMOVE_RECORDS__()
    {
        lapRecords.Clear();
    }

    public override string ToString()
    {
        return Number;
    }
}
