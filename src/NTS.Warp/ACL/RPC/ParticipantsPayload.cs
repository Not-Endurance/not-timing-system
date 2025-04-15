using NTS.Warp.ACL.Entities;

namespace NTS.Warp.ACL.RPC;

public class ParticipantsPayload
{
    public List<EmsParticipantEntry> Participants { get; set; } = new();
    public int EventId { get; set; }
}
