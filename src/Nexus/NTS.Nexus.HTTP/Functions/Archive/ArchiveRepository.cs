using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using NTS.Application.Mongo;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents.EnduranceEvents;
using NTS.Storage.Documents.EnduranceEvents.Models;

namespace NTS.Nexus.HTTP.Functions.Archive;

public class ArchiveRepository : MongoRepository<EnduranceEventDocument>, IArchiveRepository
{
    public ArchiveRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ARCHIVE_COLLECTION) { }

    protected override UpdateDefinition<EnduranceEventDocument> GetUpdateDefinition(EnduranceEventDocument document)
    {
        return Builders<EnduranceEventDocument>
            .Update.Set(x => x.Officials, document.Officials)
            .Set(x => x.Ranklists, document.Ranklists)
            .Set(x => x.EndDay, document.EndDay)
            .Set(x => x.StartDay, document.StartDay)
            .Set(x => x.City, document.City)
            .Set(x => x.Country, document.Country)
            .Set(x => x.Location, document.Location);
    }

    public async Task<IEnumerable<RankingEntryModel>> GetPerformances(int horseId)
    {
        return await GetCollection()
            .Aggregate()
            .Match(x => x.Ranklists.Any(y => y.Entries.Any(z => z.Participation.Combination.Horse.Id == horseId)))
            .Project(x =>
                x.Ranklists.SelectMany(y => y.Entries).First(z => z.Participation.Combination.Horse.Id == horseId)
            )
            .ToListAsync();
    }
}

public interface IArchiveRepository : IRepository<EnduranceEventDocument>
{
    Task<IEnumerable<RankingEntryModel>> GetPerformances(int horseId);
}
