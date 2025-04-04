using Not.Random;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Documents.Archive.Models;

public class ParticipationDocumentModel
{
    public static ParticipationDocumentModel Create(Participation participation)
    {
        var total = participation.GetTotal();

        return new ParticipationDocumentModel
        {
            Competition = CompetitionDocumentModel.Create(participation.Competition),
            Combination = CombinationDocumentModel.Create(participation.Combination),
            Phases = participation.Phases.Select(PhaseDocumentModel.Create).ToArray(),
            Total = total == null ? null : TotalDocumentModel.Create(total),
            Eliminated =
                participation.Eliminated == null ? null : EliminatedDocumentModel.Create(participation.Eliminated),
        };
    }

    public CompetitionDocumentModel Competition { get; init; } = default!;
    public CombinationDocumentModel Combination { get; init; } = default!;
    public PhaseDocumentModel[] Phases { get; init; } = default!;
    public TotalDocumentModel? Total { get; init; }
    public EliminatedDocumentModel? Eliminated { get; init; }

    public Participation ToDomain()
    {
        var competition = Competition.ToDomain();
        var combination = Combination.ToDomain();
        var phases = Phases.Select(x => x.ToDomain());
        var eliminated = Eliminated?.ToDomain();
        return new Participation(
            RandomHelper.GenerateUniqueInteger(),
            competition,
            combination,
            new(phases),
            eliminated
        );
    }
}
