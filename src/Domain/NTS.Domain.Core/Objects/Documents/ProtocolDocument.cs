using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents.Base;

namespace NTS.Domain.Core.Objects.Documents;

public record ProtocolDocument : Document
{
    public ProtocolDocument(Ranklist ranklist, EventInformation eventInformation, IEnumerable<Official> officials)
        : this(
            ranklist,
            new DocumentHeader(
                ranklist.Name,
                ranklist.Ruleset,
                eventInformation.Country,
                eventInformation.Location,
                eventInformation.EventSpan,
                officials
            )
        ) { }

    public ProtocolDocument(Ranklist ranklist, DocumentHeader header)
        : base(header)
    {
        Ranklist = ranklist;
    }

    public Ranklist Ranklist { get; }
}
