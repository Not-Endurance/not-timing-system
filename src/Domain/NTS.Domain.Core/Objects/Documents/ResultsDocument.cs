using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Results;
using NTS.Domain.Core.Objects.Documents.Base;

namespace NTS.Domain.Core.Objects.Documents;

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
                results.Ruleset,
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
