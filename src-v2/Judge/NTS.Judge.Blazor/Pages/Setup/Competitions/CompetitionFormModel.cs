using Not.Blazor.Ports;
using Not.Extensions;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Setup.Competitions;

public class CompetitionFormModel : IFormModel<Competition>
{
    private int? _requiredInspectionCompulsoryThreshold;

    public CompetitionFormModel()
    {
        //TODO: remove mock default values for testing
        Name = "Olympic Games";
    }

    public int Id { get; set; }
    public string? Name { get; set; }
    public CompetitionRuleset? Type { get; set; } = CompetitionRuleset.Regional;
    public DateTime? Day { get; set; } = DateTime.Now;
    public TimeSpan? Time { get; set; } = DateTime.Now.TimeOfDay;
    public bool UseCompulsoryThreshold { get; set; }
    public int? CompulsoryThresholdMinutes 
    {
        get => UseCompulsoryThreshold ? _requiredInspectionCompulsoryThreshold : null;
        set => _requiredInspectionCompulsoryThreshold = value;
    }
    public IReadOnlyCollection<Phase> Phases { get; private set; } = [];
    public IReadOnlyCollection<Participation> Participations { get; private set; } = [];

    public DateTimeOffset StartTime => CombineStartDayAndTime(Day, Time);

    static DateTimeOffset CombineStartDayAndTime(DateTime? startDay, TimeSpan? startTime)
    {
        var start = startDay.GetValueOrDefault(DateTime.Today);
        var startDayTime = start.Date.Add(startTime.GetValueOrDefault(DateTime.Now.TimeOfDay));
        var startTimeOffset = startDayTime.ToDateTimeOffset();
        return startTimeOffset;
    }

    public void FromEntity(Competition competition)
    {
        Id = competition.Id;
        Name = competition.Name;
        Type = competition.Type;
        DateTime? startDay = competition.Start.DateTime;
        TimeSpan? startTime = competition.Start.DateTime.TimeOfDay;
        Day = startDay;
        Time = startTime;
        Phases = competition.Phases;
        Participations = competition.Participations;
        CompulsoryThresholdMinutes = competition.CompulsoryThreshold?.Minutes;
        UseCompulsoryThreshold = competition.CompulsoryThreshold != null;
    }
}
 