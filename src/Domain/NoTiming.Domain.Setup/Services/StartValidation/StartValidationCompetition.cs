namespace NTS.Domain.Setup.Services.StartValidation;

public record StartValidationCompetition
{
    public StartValidationCompetition(int competitionId, string competitionName, string phaseSignature)
    {
        CompetitionId = competitionId;
        CompetitionName = competitionName;
        PhaseSignature = phaseSignature;
    }

    public int CompetitionId { get; }
    public string CompetitionName { get; }
    public string PhaseSignature { get; }
}
