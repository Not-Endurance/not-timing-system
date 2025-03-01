using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Documents.EnduranceEvents.Models;

public class ParticipationModel
{
    public ParticipationModel(Participation participation)
    {
        Competition = new CompetitionModel(participation.Competition);
        Combination = new CombinationModel(participation.Combination);
        Phases = participation.Phases.Select(p => new PhaseModel(p)).ToArray();
        EliminationCode = participation.Eliminated?.Code;
        EliminationReason = participation.Eliminated?.Complement;
    }

    public CompetitionModel Competition { get; init; }
    public CombinationModel Combination { get; init; }
    public PhaseModel[] Phases { get; init; }
    public string? EliminationReason { get; init; }
    public string? EliminationCode { get; init; }
}
