using NC.Structures;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Results;
using NoTiming.Domain.Core.Objects.Documents.Base;

namespace NoTiming.Domain.Core.Objects.Documents;

public sealed record ResultsDocument : Document, IIdentifiable
{
    public ResultsDocument(
        Aggregates.Result results,
        EventInformation eventInformation,
        IEnumerable<Official> officials
    )
        : this(
            results,
            new DocumentHeader(
                results.Name,
                results.EnglishNames,
                eventInformation.Country,
                eventInformation.Location,
                eventInformation.EventSpan,
                officials
            )
        ) { }

    public ResultsDocument(Aggregates.Result results, DocumentHeader header)
        : base(header)
    {
        Results = results;
    }

    public Aggregates.Result Results { get; }
    public int Id => Results.Id;
    public bool IsRanked => Results.IsRanked;
    public IReadOnlyList<ParticipationResult> Entries => Results.Entries;
}
