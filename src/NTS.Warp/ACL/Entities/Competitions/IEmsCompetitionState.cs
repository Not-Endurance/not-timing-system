using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Competitions;

public interface IEmsCompetitionState : IEmsIdentifiable
{
    EmsCompetitionType Type { get; }
    string Name { get; }
    DateTime StartTime { get; }
    string FeiCategoryEventNumber { get; }
    string FeiScheduleNumber { get; }
    string Rule { get; }
    string EventCode { get; }
}
