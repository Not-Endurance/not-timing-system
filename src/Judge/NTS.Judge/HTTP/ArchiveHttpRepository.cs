using Not.Application.HTTP;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.HTTP;

public class ArchiveHttpRepository : HttpRepository<ArchiveEntry>
{
    public ArchiveHttpRepository(NHttpClient client)
        : base("archive", client) { }
}
