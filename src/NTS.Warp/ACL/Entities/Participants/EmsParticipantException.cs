using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Participants;

public class EmsParticipantException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsParticipant);
}
