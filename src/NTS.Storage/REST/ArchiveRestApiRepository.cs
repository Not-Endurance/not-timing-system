using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.REST;

public class ArchiveRestApiRepository : RestApiRepository2<ArchiveEntry, ArchiveEntryModel>, ITransient
{
    public ArchiveRestApiRepository(NHttpClient client)
        : base("archive", client)
    {}
}
