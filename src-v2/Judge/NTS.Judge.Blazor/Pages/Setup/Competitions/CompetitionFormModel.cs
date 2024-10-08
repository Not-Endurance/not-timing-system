using Not.Blazor.Ports;
using Not.Extensions;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Setup.Competitions;

public class CompetitionFormModel : IFormModel<Competition>
{
    private int? _compulsoryRequiredInspectionSpan;

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
    public bool UseAutomaticCRI { get; set; }
    public int? CRIRecovery 
    {
        get => UseAutomaticCRI ? _compulsoryRequiredInspectionSpan : null;
        set => _compulsoryRequiredInspectionSpan = value;
    }
    public IReadOnlyCollection<Phase> Phases { get; private set; } = [];
    public IReadOnlyCollection<Contestant> Contestants { get; private set; } = [];

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
        DateTime? startDay = competition.StartTime.DateTime;
        TimeSpan? startTime = competition.StartTime.DateTime.TimeOfDay;
        Day = startDay;
        Time = startTime;
        Phases = competition.Phases;
        Contestants = competition.Contestants;
        CRIRecovery = competition.CriRecovery;
        UseAutomaticCRI = competition.CriRecovery != null;
    }
}
 