using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents.Base;

namespace NTS.Domain.Core.Objects.Documents;

public record ProtocolDocument : Document
{
    public ProtocolDocument(Ranklist ranklist, EnduranceEvent enduranceEvent, IEnumerable<Official> officials)
        : base(
            new DocumentHeader(
                ranklist.Name,
                enduranceEvent.Country,
                enduranceEvent.Location,
                enduranceEvent.EventSpan,
                officials
            )
        )
    {
        Ranklist = ranklist;
    }

    public Ranklist Ranklist { get; }
}
