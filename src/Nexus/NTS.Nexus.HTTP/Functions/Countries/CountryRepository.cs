using MongoDB.Driver;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents.Countries;

namespace NTS.Nexus.HTTP.Functions.Countries;

public class CountryRepository : MongoRepository<CountryDocument>
{
    public CountryRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.COUNTRIES_COLLECTION) { }

    protected override UpdateDefinition<CountryDocument> GetUpdateDefinition(
        CountryDocument document
    )
    {
        return Builders<CountryDocument>
            .Update.Set(x => x.IsoCode, document.IsoCode)
            .Set(x => x.NfCode, document.NfCode)
            .Set(x => x.Name, document.Name);
    }
}
