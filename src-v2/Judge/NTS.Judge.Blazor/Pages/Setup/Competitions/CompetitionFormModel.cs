using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Setup.Competitions;

public class CompetitionFormModel
{

    public CompetitionFormModel()
    {
        //TODO: remove mock default values for testing
        Name = "Olympic Games";
    }
    public CompetitionFormModel(Competition competition)
    {
        Id = competition.Id;
        Name = competition.Name;
        Type = competition.Type;
        Ruleset = competition.Ruleset;
        DateTime? startDay = competition.StartTime.DateTime;
        TimeSpan? startTime = competition.StartTime.DateTime.TimeOfDay;
        StartDay = startDay;
        StartTime = startTime;
        Phases = competition.Phases;
        Contestants = competition.Contestants;
        CRIRecovery = competition.CriRecovery;
        UseAutomaticCRI = competition.CriRecovery != null;
    }

    public int? Id { get; set; }
    public string Name { get; set; }
    public CompetitionRuleset Ruleset { get; set; } = CompetitionRuleset.Regional;
    public CompetitionType Type { get; set; } = CompetitionType.Qualification;
    public DateTime? StartDay { get; set; } = DateTime.Now;
    public TimeSpan? StartTime { get; set; } = DateTime.Now.TimeOfDay;
    public bool UseAutomaticCRI { get; set; }
    public int? CRIRecovery { get; set;  }
    public IReadOnlyCollection<Phase>? Phases { get; }
    public IReadOnlyCollection<Contestant>? Contestants { get; }
}
 