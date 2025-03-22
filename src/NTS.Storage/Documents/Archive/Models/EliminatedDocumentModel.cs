using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Storage.Documents.Archive.Models;

public class EliminatedDocumentModel
{
    public EliminatedDocumentModel(Eliminated eliminated)
    {
        Code = eliminated.Code;
        Reason = eliminated.Complement;
        if (eliminated is FailedToQualify ftq)
        {
            FtqCodes = ftq.FtqCodes.ToArray();
        }
    }

    public string Code { get; init; }
    public string? Reason { get; init; }
    public FtqCode[]? FtqCodes { get; init; }
}
