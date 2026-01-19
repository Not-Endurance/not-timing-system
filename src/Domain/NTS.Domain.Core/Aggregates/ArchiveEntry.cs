using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Aggregates;

public class ArchiveEntry : Aggregate
{
    public ArchiveEntry(EnduranceEvent enduranceEvent, IEnumerable<Official> officials, IEnumerable<Ranklist> ranklists)
        : base(GenerateId())
    {
        EnduranceEvent = enduranceEvent;
        Officials = officials.ToList();
        Ranklists = ranklists.ToList();
    }

    public string TenantId { get; set; } = default!;
    public EnduranceEvent EnduranceEvent { get; set; }
    public IReadOnlyList<Official> Officials { get; }
    public IReadOnlyList<Ranklist> Ranklists { get; }
}
