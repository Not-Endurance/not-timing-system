using System.Diagnostics;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Setup;
using NoTiming.Application.Contracts.Setup.Models;

namespace NoTiming.Api.Application.Mongo.Repositories;

public class HorseMongoRepository : MongoRepository<HorseModel>
{
    readonly ITelemetryService _telemetry;

    public HorseMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.HORSES_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<HorseModel> GetUpdateDefinition(HorseModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(HorseMongoRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<HorseModel>
                .Update.Set(x => x.Name, document.Name)
                .Set(x => x.NameEnglish, document.NameEnglish)
                .Set(x => x.FeiId, document.FeiId);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
