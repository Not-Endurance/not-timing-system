using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Setup;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class HorseRepository : MongoRepository<HorseModel>
{
    public HorseRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.HORSES_COLLECTION) { }

    protected override UpdateDefinition<HorseModel> GetUpdateDefinition(HorseModel document)
    {
        return Builders<HorseModel>.Update.Set(x => x.Name, document.Name).Set(x => x.FeiId, document.FeiId);
    }
}
