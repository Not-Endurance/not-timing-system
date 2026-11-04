namespace NTS.Domain.Core.Objects.Presentlists;

public record PresentlistEntry : ValueObject
{
    internal PresentlistEntry(
        int number,
        int phaseId,
        string athleteName,
        string? athleteNameEnglish,
        CompetitionRuleset ruleset,
        Timestamp time,
        PresentlistEntryType type
    )
    {
        Number = number;
        PhaseId = phaseId;
        AthleteName = athleteName;
        AthleteNameEnglish = athleteNameEnglish;
        Ruleset = ruleset;
        Time = time;
        Type = type;
    }

    public int Number { get; }
    public int PhaseId { get; }
    public string AthleteName { get; }
    public string? AthleteNameEnglish { get; }
    public CompetitionRuleset Ruleset { get; }
    public Timestamp Time { get; }
    public PresentlistEntryType Type { get; }
    public PresentlistEntryKey Key => new(Number, PhaseId, Type);
}
