using System.Diagnostics;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Setup;
using NoTiming.Application.Contracts.Setup.Models;

namespace NoTiming.Api.Application.Mongo.Repositories;

public class AthleteMongoRepository : MongoRepository<AthleteModel>
{
    readonly ITelemetryService _telemetry;

    public AthleteMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ATHLETES_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<AthleteModel> GetUpdateDefinition(AthleteModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(AthleteMongoRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<AthleteModel>
                .Update.Set(x => x.Name, document.Name)
                .Set(x => x.NameEnglish, document.NameEnglish)
                .Set(x => x.Club, document.Club)
                .Set(x => x.Country, document.Country)
                .Set(x => x.FeiId, document.FeiId)
                .Set(x => x.User, document.User);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
