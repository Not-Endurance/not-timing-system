using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.LapRecords;

public interface IEmsLapRecordState : IEmsIdentifiable
{
    DateTime StartTime { get; }
    DateTime? ArrivalTime { get; }
    DateTime? InspectionTime { get; }
    DateTime? ReInspectionTime { get; }
    bool IsReinspectionRequired { get; }
    bool IsRequiredInspectionRequired { get; }
}
