using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Storage.Documents.Archive.Models;

public class ParticipationDocumentModel
{
    public ParticipationDocumentModel(Participation participation)
    {
        Competition = new CompetitionDocumentModel(participation.Competition);
        Combination = new CombinationDocumentModel(participation.Combination);
        Phases = participation.Phases.Select(p => new PhaseDocumentModel(p)).ToArray();
        Eliminated = participation.Eliminated == null ? null : new EliminatedDocumentModel(participation.Eliminated);
    }

    public CompetitionDocumentModel Competition { get; init; }
    public CombinationDocumentModel Combination { get; init; }
    public PhaseDocumentModel[] Phases { get; init; }
    public EliminatedDocumentModel? Eliminated { get; init; }
}
