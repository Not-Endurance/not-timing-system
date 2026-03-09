namespace NTS.Domain.Setup.Services.StartValidation;

public class StartValidationIssue
{
    readonly IReadOnlyList<StartValidationCompetition> _competitions;

    public StartValidationIssue(
        int participationNumber,
        string athleteName,
        string horseName,
        IEnumerable<StartValidationCompetition> competitions
    )
    {
        ParticipationNumber = participationNumber;
        AthleteName = athleteName;
        HorseName = horseName;
        _competitions = competitions.ToList().AsReadOnly();
    }

    public int ParticipationNumber { get; }
    public string AthleteName { get; }
    public string HorseName { get; }
    public IReadOnlyList<StartValidationCompetition> Competitions => _competitions;
}
