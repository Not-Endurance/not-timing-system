using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Shared;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class CountryRepository : MongoRepository<CountryModel>
{
    public CountryRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.COUNTRIES_COLLECTION) { }

    protected override UpdateDefinition<CountryModel> GetUpdateDefinition(CountryModel document)
    {
        return Builders<CountryModel>
            .Update.Set(x => x.IsoCode, document.IsoCode)
            .Set(x => x.NfCode, document.NfCode)
            .Set(x => x.Name, document.Name);
    }
}
