namespace NTS.Domain.Setup.Services.StartValidation;

public record StartValidationCompetition
{
    public StartValidationCompetition(Guid competitionId, string competitionName, string phaseSignature)
    {
        CompetitionId = competitionId;
        CompetitionName = competitionName;
        PhaseSignature = phaseSignature;
    }

    public Guid CompetitionId { get; }
    public string CompetitionName { get; }
    public string PhaseSignature { get; }
}
