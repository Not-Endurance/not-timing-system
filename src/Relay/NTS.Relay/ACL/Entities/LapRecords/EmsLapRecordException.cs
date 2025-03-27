using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.LapRecords;

public class EmsLapRecordException : EmsDomainExceptionBase
{
    protected override string Entity => "Lap";
}
