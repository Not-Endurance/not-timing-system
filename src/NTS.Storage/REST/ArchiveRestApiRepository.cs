using Not.Application.HTTP;
using Not.Serialization.JSON;
using Not.Storage.REST;
using NTS.Application.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.REST;

public class ArchiveRestApiRepository : RestApiRepository<ArchiveEntry>
{
    readonly NHttpClient _client;

    public ArchiveRestApiRepository(NHttpClient client)
        : base("archive", client)
    {
        _client = client;
    }

    // TODO: Convert other Nexus functions to use Models instead of Aggregates, then adjust RestApiRepository and remove these methods
    public async Task<IEnumerable<ArchiveEntry>> GetEntries()
    {
        var contents = await _client.Get("archive");
        if (contents == null)
        {
            return [];
        }
        return contents.FromJson<IEnumerable<ArchiveModel>>().Select(x => x.MapToDomain());
    }

    public async Task<ArchiveEntry?> GetEntry(int id)
    {
        var contents = await _client.Get("archive");
        if (contents == null)
        {
            return null;
        }
        var documents = contents.FromJson<IEnumerable<ArchiveModel>>();
        var document = documents.FirstOrDefault(x => x.Id == id);
        return document?.MapToDomain();
    }
}
