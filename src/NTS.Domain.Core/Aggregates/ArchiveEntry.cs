using Not.Domain.Base;

namespace NTS.Domain.Core.Aggregates;

public class ArchiveEntry : AggregateRoot
{
    public ArchiveEntry(
        EnduranceEvent enduranceEvent,
        IEnumerable<Official> officials,
        IEnumerable<Ranking> rankings
    ) 
        : base(GenerateId())
    {
        EnduranceEvent = enduranceEvent;
        Officials = officials.ToList();
        Rankings = rankings.ToList();
    }

    public string TenantId { get; set; } = default!;
    public EnduranceEvent EnduranceEvent { get; set; }
    public IReadOnlyList<Official> Officials { get; }
    public IReadOnlyList<Ranking> Rankings { get; }
}
