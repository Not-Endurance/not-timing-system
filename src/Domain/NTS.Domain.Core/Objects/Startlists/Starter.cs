using Not.Formatting;
using NTS.Domain.Helpers;

namespace NTS.Domain.Core.Objects.Startlists;

public record Starter : ValueObject
{
    internal Starter(
        string athleteName,
        string? athleteNameEnglish,
        CompetitionRuleset ruleset,
        int number,
        int phaseNumber,
        string gate,
        double distance,
        Timestamp start
    )
    {
        AthleteName = athleteName;
        AthleteNameEnglish = athleteNameEnglish;
        Ruleset = ruleset;
        Number = number;
        PhaseNumber = phaseNumber;
        Gate = gate;
        Distance = distance;
        Start = start;
    }

    public string AthleteName { get; }
    public string? AthleteNameEnglish { get; }
    public CompetitionRuleset Ruleset { get; }
    public int Number { get; }
    public int PhaseNumber { get; }
    public string Gate { get; }
    public double Distance { get; }
    public StartlistEntryState State { get; internal set; } = StartlistEntryState.Resting;
    public Timestamp Start { get; }

    public override string ToString()
    {
        var distance = Distance + km_string;
        var startTime = Start.ToTimeSpan();
        var start = FormattingHelper.Format(startTime);
        return Combine(Number, NameRenderingHelper.Render(AthleteName, AthleteNameEnglish, Ruleset), distance, start);
    }
}
