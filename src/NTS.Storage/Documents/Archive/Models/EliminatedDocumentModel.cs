using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Storage.Documents.Archive.Models;

public class EliminatedDocumentModel
{
    public static EliminatedDocumentModel Create(Eliminated eliminated)
    {
        if (eliminated is FailedToQualify ftq)
        {
            return new EliminatedDocumentModel
            {
                Code = eliminated.Code,
                Reason = eliminated.Complement,
                FtqCodes = ftq.FtqCodes.ToArray(),
            };
        }
        return new EliminatedDocumentModel { Code = eliminated.Code, Reason = eliminated.Complement };
    }

    public string Code { get; init; } = default!;
    public string? Reason { get; init; }
    public FtqCode[]? FtqCodes { get; init; }

    public Eliminated ToDomain()
    {
        return Code switch // TODO refactor Eliminated to non-abstract and only FTQ as separate class
        {
            Eliminated.FAILED_TO_QUALIFY => new FailedToQualify(FtqCodes!, Reason),
            Eliminated.WITHDRAWN => new Withdrawn(),
            Eliminated.DISQUALIFIED => new Disqualified(Reason!),
            Eliminated.FINISHED_NOT_RANKED => new FinishedNotRanked(Reason!),
            Eliminated.RETIRED => new Retired(),
            _ => throw new NotImplementedException(),
        };
    }
}
