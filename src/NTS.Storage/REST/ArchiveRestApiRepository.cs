using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.REST;

public class ArchiveRestApiRepository : RestApiRepository<ArchiveEntry, ArchiveEntryModel>, ITransient
{
    public ArchiveRestApiRepository(NHttpClient client)
        : base("archive", client) { }
}
