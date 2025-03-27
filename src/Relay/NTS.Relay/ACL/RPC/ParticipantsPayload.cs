using NTS.Relay.ACL.Entities;

namespace NTS.Relay.ACL.RPC;

public class ParticipantsPayload
{
    public List<EmsParticipantEntry> Participants { get; set; } = new();
    public int EventId { get; set; }
}
