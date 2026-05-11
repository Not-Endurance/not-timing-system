namespace NTS.Domain.Setup.Services.StartValidation;

public class StartValidationIssue
{
    readonly IReadOnlyList<StartValidationCompetition> _competitions;

    public StartValidationIssue(
        int? participationNumber,
        string? athleteName,
        string? horseName,
        string summary,
        IEnumerable<StartValidationCompetition> competitions
    )
    {
        ParticipationNumber = participationNumber;
        AthleteName = athleteName;
        HorseName = horseName;
        Summary = summary;
        _competitions = competitions.ToList().AsReadOnly();
    }

    public StartValidationIssue(
        int participationNumber,
        string athleteName,
        string horseName,
        IEnumerable<StartValidationCompetition> competitions
    )
        : this(
            participationNumber,
            athleteName,
            horseName,
            $"#{participationNumber}: {athleteName}, {horseName}",
            competitions
        ) { }

    public StartValidationIssue(string summary)
        : this(null, null, null, summary, []) { }

    public int? ParticipationNumber { get; }
    public string? AthleteName { get; }
    public string? HorseName { get; }
    public string Summary { get; }
    public IReadOnlyList<StartValidationCompetition> Competitions => _competitions;
    public bool IsAutoCorrectable => ParticipationNumber.HasValue && Competitions.Count > 1;
}
