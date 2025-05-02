using Not.Blazor.CRUD.Forms.Ports;
using Not.Domain.Exceptions;
using Not.Extensions;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Competitions;

public class CompetitionFormModel : IFormModel<Competition>
{
    int? _requiredInspectionCompulsoryThreshold;

    public CompetitionFormModel()
    {
        UseCompulsoryThreshold = true;
        CompulsoryThresholdMinutes = 10;
#if DEBUG
        Name = "Olympic Games";
#endif
    }

    public int? Id { get; set; }
    public string? Name { get; set; }
    public CompetitionType Type { get; set; } = CompetitionType.Qualification;
    public CompetitionRuleset Ruleset { get; set; } = CompetitionRuleset.Regional;
    public DateTime? Day { get; set; } = DateTime.Now;
    public TimeSpan? Time { get; set; } = DateTime.Now.TimeOfDay;
    public bool UseCompulsoryThreshold { get; set; }
    public int? CompulsoryThresholdMinutes
    {
        get => UseCompulsoryThreshold ? _requiredInspectionCompulsoryThreshold : null;
        set => _requiredInspectionCompulsoryThreshold = value;
    }
    public string? FeiRule { get; set; }
    public string? FeiEventCode { get; set; }
    public string? FeiScheduleNumber { get; set; }
    public string? FeiCategoryEventNumber { get; set; }
    public IReadOnlyCollection<Phase> Phases { get; private set; } = [];
    public IReadOnlyCollection<Participation> Participations { get; private set; } = [];
    public DateTimeOffset StartTime => CombineStartDayAndTime(Day, Time);

    public void FromEntity(Competition competition)
    {
        Id = competition.Id;
        Name = competition.Name;
        Type = competition.Type;
        Day = competition.Start.ToLocalTime().DateTime; // TODO: Create NComponent that's using DateTimeOffset and convert to DateTime correctly
        Time = competition.Start.ToLocalTime().DateTime.TimeOfDay;
        Ruleset = competition.Ruleset;
        Phases = competition.Phases;
        Participations = competition.Participations;
        CompulsoryThresholdMinutes = competition.CompulsoryThresholdSpan?.Minutes;
        UseCompulsoryThreshold = competition.CompulsoryThresholdSpan != null;
        FeiRule = competition.FeiRule;
        FeiEventCode = competition.FeiEventCode;
        FeiScheduleNumber = competition.FeiScheduleNumber;
        FeiCategoryEventNumber = competition.FeiCategoryEventNumber;
    }

    static DateTimeOffset CombineStartDayAndTime(DateTime? startDay, TimeSpan? startTime)
    {
        var today = startDay.GetValueOrDefault(DateTime.Today);
        if(startTime == null)
        {
            throw new DomainPropertyException(nameof(Time), Null_or_malformed_string, Time_string);
        }
        var nowTime = startTime.GetValueOrDefault(DateTime.Now.TimeOfDay);
        var startDayTime = today.Date.Add(nowTime);
        var startTimeOffset = startDayTime.ToDateTimeOffset();
        return startTimeOffset;
    }
}
