using System.ComponentModel;
using NTS.Relay.ACL.Abstractions;
using NTS.Relay.ACL.Entities.Laps;
using NTS.Relay.ACL.Entities.Participants;
using NTS.Relay.ACL.Entities.Results;

namespace NTS.Relay.ACL.Entities.LapRecords;

public class EmsLapRecord : EmsDomainBase<EmsLapRecordException>
{
    DateTime? arrivalTime;
    DateTime? inspectionTime;
    DateTime? reInspectionTime;

    [Newtonsoft.Json.JsonConstructor]
    public EmsLapRecord() { }

    public EmsLapRecord(DateTime startTime, EmsLap lap)
        : base(GENERATE_ID)
    {
        StartTime = startTime;
        Lap = lap;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public EmsLap Lap { get; private set; }
    public DateTime StartTime { get; set; } // TODO: set to private/internal after testing
    public DateTime? ArrivalTime
    {
        get => arrivalTime;
        set => SetProperty(ref this.arrivalTime, value, nameof(this.ArrivalTime)); // TODO: set to private/internal after testing
    }
    public DateTime? InspectionTime
    {
        get => inspectionTime;
        set => SetProperty(ref this.inspectionTime, value, nameof(this.InspectionTime)); // TODO: set to private/internal after testing
    }
    public DateTime? ReInspectionTime
    {
        get => reInspectionTime;
        set => SetProperty(ref this.reInspectionTime, value, nameof(this.ReInspectionTime)); // TODO: set to private/internal after testing
    }
    public bool IsReinspectionRequired { get; internal set; }
    public bool IsRequiredInspectionRequired { get; internal set; }
    public EmsResult Result { get; internal set; }
    public DateTime? VetGateTime => ReInspectionTime ?? InspectionTime;
    public DateTime? NextStarTime => Lap.IsFinal ? null : VetGateTime?.AddMinutes(Lap.RestTimeInMins);
    public Dictionary<WitnessEventType, EmsRfidTag> Detected { get; private set; } = [];

    protected virtual void RaisePropertyChanged(string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    void SetProperty<T>(ref T property, T value, string name)
    {
        if (!value.Equals(property))
        {
            property = value;
            RaisePropertyChanged(name);
        }
    }
}

public enum WitnessEventType
{
    Invalid = 0,
    VetIn = 1,
    Arrival = 2,
}
