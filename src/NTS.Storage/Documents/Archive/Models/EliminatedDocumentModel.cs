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
}
