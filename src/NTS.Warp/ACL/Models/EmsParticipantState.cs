using NTS.Relay.ACL.Entities.Participants;

namespace NTS.Relay.ACL.Models;

public class EmsParticipantState : IEmsParticipantState
{
    public bool Unranked { get; set; }
    public string Number { get; set; }
    public int? MaxAverageSpeedInKmPh { get; set; }
    public int Id { get; set; }
}
