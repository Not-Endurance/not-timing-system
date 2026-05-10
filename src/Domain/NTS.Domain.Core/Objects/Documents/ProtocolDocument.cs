using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents.Base;

namespace NTS.Domain.Core.Objects.Documents;

public record ProtocolDocument : Document
{
    public ProtocolDocument(Ranklist ranklist, EventInformation eventInformation, IEnumerable<Official> officials)
        : base(
            new DocumentHeader(
                ranklist.Name,
                eventInformation.Country,
                eventInformation.Location,
                eventInformation.EventSpan,
                officials
            )
        )
    {
        Ranklist = ranklist;
    }

    public Ranklist Ranklist { get; }
}
