using NTS.Warp.ACL.Entities.Competitions;

namespace NTS.Warp.ACL.Models;

public class EmsCompetitionState : IEmsCompetitionState
{
    public EmsCompetitionType Type { get; set; }
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
    public string FeiCategoryEventNumber { get; set; }
    public string FeiScheduleNumber { get; set; }
    public string Rule { get; set; }
    public string EventCode { get; set; }
    public int Id { get; set; }
}
