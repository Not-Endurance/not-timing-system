using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Shared;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class CountryRepository : MongoRepository<CountryModel>
{
    readonly ITelemetryService _telemetry;

    public CountryRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.COUNTRIES_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<CountryModel> GetUpdateDefinition(CountryModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(CountryRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<CountryModel>
                .Update.Set(x => x.IsoCode, document.IsoCode)
                .Set(x => x.NfCode, document.NfCode)
                .Set(x => x.Name, document.Name);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
