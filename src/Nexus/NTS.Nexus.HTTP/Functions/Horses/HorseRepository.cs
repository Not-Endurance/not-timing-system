using MongoDB.Driver;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents.Horses;

namespace NTS.Nexus.HTTP.Functions.Horses;

public class HorseRepository : MongoRepository<HorseDocument>
{
    public HorseRepository() : base(MongoConstants.NTS_DATABASE, MongoConstants.HORSES_COLLECTION)
    {
    }

    protected override UpdateDefinition<HorseDocument> GetUpdateDefinition(HorseDocument document)
    {
        return Builders<HorseDocument>.Update
            .Set(x => x.Name, document.Name)
            .Set(x => x.FeiId, document.FeiId);
    }
}
