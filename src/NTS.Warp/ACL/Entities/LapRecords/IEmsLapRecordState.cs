using NTS.Relay.ACL.Abstractions;

namespace Core.Domain.State.LapRecords;

public interface IEmsLapRecordState : IEmsIdentifiable
{
    DateTime StartTime { get; }
    DateTime? ArrivalTime { get; }
    DateTime? InspectionTime { get; }
    DateTime? ReInspectionTime { get; }
    bool IsReinspectionRequired { get; }
    bool IsRequiredInspectionRequired { get; }
}
