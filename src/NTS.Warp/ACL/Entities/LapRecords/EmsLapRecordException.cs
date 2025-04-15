using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.LapRecords;

public class EmsLapRecordException : EmsDomainExceptionBase
{
    protected override string Entity => "Lap";
}
