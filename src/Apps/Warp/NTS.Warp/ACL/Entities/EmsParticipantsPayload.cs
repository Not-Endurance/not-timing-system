namespace NTS.Warp.ACL.Entities;

public class EmsParticipantsPayload
{
    public List<EmsParticipantEntry> Participants { get; set; } = [];
    public int EventId { get; set; }
}
