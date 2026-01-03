using MongoDB.Driver;
using NTS.Application.Models;
using NTS.Application.Mongo;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Nexus.HTTP.Functions.Clubs;

public class ClubRepository : MongoRepository<ClubModel>
{
    public ClubRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.CLUBS_COLLECTION) { }

    protected override UpdateDefinition<ClubModel> GetUpdateDefinition(ClubModel document)
    {
        return Builders<ClubModel>.Update.Set(x => x.Name, document.Name);
    }
}
