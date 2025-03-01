using MongoDB.Driver;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents.Clubs;

namespace NTS.Nexus.HTTP.Functions.Clubs;

public class ClubRepository : MongoRepository<ClubDocument>
{
    public ClubRepository()
        : base(MongoConstants.NTS_DATABASE, MongoConstants.CLUBS_COLLECTION) { }

    protected override UpdateDefinition<ClubDocument> GetUpdateDefinition(ClubDocument document)
    {
        return Builders<ClubDocument>.Update.Set(x => x.Name, document.Name);
    }
}
