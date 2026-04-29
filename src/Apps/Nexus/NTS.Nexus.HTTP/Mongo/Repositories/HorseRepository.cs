using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class HorseRepository : MongoRepository<HorseModel>
{
    readonly ITelemetryService _telemetry;

    public HorseRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.HORSES_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<HorseModel> GetUpdateDefinition(HorseModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(HorseRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<HorseModel>.Update.Set(x => x.Name, document.Name).Set(x => x.FeiId, document.FeiId);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
