using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.Documents.Archive;

namespace NTS.Judge.HTTP;

public class ArchiveHttpRepository : HttpRepository<ArchiveEntry>, IArchiveRepository
{
    readonly NHttpClient _client;

    public ArchiveHttpRepository(NHttpClient client)
        : base("archive", client)
    {
        _client = client;
    }

    public async Task<ArchiveEntry?> GetEntry(int id)
    {
        var contents = await _client.Get("archive");
        if (contents == null)
        {
            return null;
        }
        var documents = contents.FromJson<IEnumerable<ArchiveDocument>>();
        var document = documents.FirstOrDefault(x => x.Id == id);
        if (document == null)
        {
            return null;
        }
        return document.ToDomain();
    }
}

public interface IArchiveRepository : IRepository<ArchiveEntry>
{
    Task<ArchiveEntry?> GetEntry(int id);
}
