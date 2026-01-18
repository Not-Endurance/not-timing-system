using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Setup;

namespace NTS.Storage.Mongo.Repositories;

public class ClubRepository : MongoRepository<ClubModel>
{
    public ClubRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.CLUBS_COLLECTION) { }

    protected override UpdateDefinition<ClubModel> GetUpdateDefinition(ClubModel document)
    {
        return Builders<ClubModel>.Update.Set(x => x.Name, document.Name);
    }
}
