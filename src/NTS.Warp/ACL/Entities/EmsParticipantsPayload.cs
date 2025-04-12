using NTS.Relay.ACL.Entities.EMS;

namespace NTS.Relay.ACL.Entities;

public class EmsParticipantsPayload
{
    public List<EmsParticipantEntry> Participants { get; set; } = [];
    public int EventId { get; set; }
}
