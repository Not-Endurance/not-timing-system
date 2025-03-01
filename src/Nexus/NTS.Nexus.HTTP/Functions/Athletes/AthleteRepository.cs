using MongoDB.Driver;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents.Athletes;

namespace NTS.Nexus.HTTP.Functions.Athletes;

public class AthleteRepository : MongoRepository<AthleteDocument>
{
    public AthleteRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ATHLETES_COLLECTION) { }

    protected override UpdateDefinition<AthleteDocument> GetUpdateDefinition(
        AthleteDocument document
    )
    {
        return Builders<AthleteDocument>
            .Update.Set(x => x.Names, document.Names)
            .Set(x => x.Category, document.Category)
            .Set(x => x.Club, document.Club)
            .Set(x => x.Country, document.Country)
            .Set(x => x.FeiId, document.FeiId);
    }
}
