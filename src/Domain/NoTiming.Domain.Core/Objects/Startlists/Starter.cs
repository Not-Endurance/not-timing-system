using NC.Formatting;
using NoTiming.Domain.Helpers;

namespace NoTiming.Domain.Core.Objects.Startlists;

public record Starter : ValueObject
{
    internal Starter(
        string athleteName,
        string? athleteNameEnglish,
        bool englishNames,
        int number,
        int phaseNumber,
        string gate,
        double distance,
        Timestamp start
    )
    {
        AthleteName = athleteName;
        AthleteNameEnglish = athleteNameEnglish;
        EnglishNames = englishNames;
        Number = number;
        PhaseNumber = phaseNumber;
        Gate = gate;
        Distance = distance;
        Start = start;
    }

    public string AthleteName { get; }
    public string? AthleteNameEnglish { get; }
    public bool EnglishNames { get; }
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
        return Combine(
            Number,
            NameRenderingHelper.Render(AthleteName, AthleteNameEnglish, EnglishNames),
            distance,
            start
        );
    }
}
