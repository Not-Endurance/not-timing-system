using System.Data;
using NTS.Relay.ACL.Abstractions;
using NTS.Relay.ACL.Entities.Laps;

namespace NTS.Relay.ACL.Entities.Competitions;

public class EmsCompetition : EmsDomainBase<EmsCompetitionException>
{
    List<EmsLap> _laps = [];

    [Newtonsoft.Json.JsonConstructor]
    EmsCompetition() { }

    public EmsCompetition(EmsCompetitionType type, string name)
        : base(GENERATE_ID)
    {
        Type = type;
        Name = name;
        StartTime = DateTime.Now;
    }

    public EmsCompetition(IEmsCompetitionState state)
        : base(GENERATE_ID)
    {
        Type = state.Type;
        Name = state.Name;
        StartTime = state.StartTime;
        FeiScheduleNumber = state.FeiScheduleNumber;
        FeiCategoryEventNumber = state.FeiCategoryEventNumber;
        EventCode = state.EventCode;
        Rule = state.Rule;
    }

    public EmsCompetitionType Type { get; internal set; }
    public string Name { get; internal set; }

    /// <summary>
    /// Used to build EnduranceEvent.FEIID and Competition.FEIID. Its located at EVENT DETAIL (Search Venue->Show->ShowName->EventCode) upper right, https://data.fei.org
    /// </summary>
    public string FeiCategoryEventNumber { get; internal set; }

    /// <summary>
    /// Used to Competition.FEIID. Its located at COMPETITION DETAIL (Search Venue->Show->ShowName->EventCode->CompetitionName), upper right, https://data.fei.org
    /// </summary>
    public string FeiScheduleNumber { get; internal set; } = string.Empty;
    public string EventCode { get; internal set; }
    public string Rule { get; internal set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public IReadOnlyList<EmsLap> Laps
    {
        get => _laps.OrderBy(x => x.OrderBy).ToList().AsReadOnly();
        private set => _laps = value.ToList();
    }

    public void Save(EmsLap lap)
    {
        if (_laps.Contains(lap))
        {
            _laps.Remove(lap);
        }
        _laps.Add(lap);
    }
}

public enum EmsCompetitionType
{
    Invalid = 0,
    National = 1,
    International = 2,
}
