using NTS.Relay.ACL.Entities.LapRecords;

namespace NTS.Relay.ACL.Entities;

public class EmsWitnessEvent
{
    public WitnessEventType Type { get; set; }
    public string TagId { get; set; }
    public DateTime Time { get; set; }
    public bool IsFromWitnessApp { get; set; }
}
