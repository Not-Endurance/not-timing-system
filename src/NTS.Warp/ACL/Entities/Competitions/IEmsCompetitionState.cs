using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Competitions;

public interface IEmsCompetitionState : IEmsIdentifiable
{
    EmsCompetitionType Type { get; }
    string Name { get; }
    DateTimeOffset StartTime { get; }
    string FeiCategoryEventNumber { get; }
    string FeiScheduleNumber { get; }
    string Rule { get; }
    string EventCode { get; }
}
