using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects;

public record RanklistDocument : Document
{
    public RanklistDocument(Ranklist ranklist, EnduranceEvent enduranceEvent, IEnumerable<Official> officials)
        : base(new DocumentHeader(ranklist.Name, enduranceEvent.PopulatedPlace, enduranceEvent.EventSpan, officials))
    {
        Ranklist = ranklist;
    }

    public Ranklist Ranklist { get; private set; }
}
