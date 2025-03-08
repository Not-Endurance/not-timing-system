using MongoDB.Driver;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents.Settings;

namespace NTS.Nexus.HTTP.Functions.Settings;

public class SettingRepository : MongoRepository<SettingDocument>
{
    public SettingRepository(IMongoContext context) : base(context, "nts", "settings")
    {
    }

    protected override UpdateDefinition<SettingDocument> GetUpdateDefinition(SettingDocument document)
    {
        return Builders<SettingDocument>.Update
            .Set(x => x.DetectionMode, document.DetectionMode)
            .Set(x => x.Country, document.Country);
    }
}
