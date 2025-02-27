namespace NTS.Domain.Core.Aggregates;

public class ArchiveEntry : IAggregateRoot
{
    public ArchiveEntry(EnduranceEvent enduranceEvent, IReadOnlyList<Official> officials, IReadOnlyList<Ranking> rankings)
    {
        EnduranceEvent = enduranceEvent;
        Officials = officials;
        Rankings = rankings;
    }

    public string TenantId { get; set; } = default!;
    public EnduranceEvent EnduranceEvent { get; set; }
    public IReadOnlyList<Official> Officials { get; }
    public IReadOnlyList<Ranking> Rankings { get; }
}
