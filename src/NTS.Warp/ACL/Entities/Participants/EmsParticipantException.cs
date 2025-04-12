using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Participants;

public class EmsParticipantException : EmsDomainExceptionBase
{
    protected override string Entity { get; } = nameof(EmsParticipant);
}
