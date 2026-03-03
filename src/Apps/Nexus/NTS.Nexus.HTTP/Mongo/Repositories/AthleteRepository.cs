using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Setup;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class AthleteRepository : MongoRepository<AthleteModel>
{
    readonly ITelemetryService _telemetry;

    public AthleteRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ATHLETES_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<AthleteModel> GetUpdateDefinition(AthleteModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(AthleteRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<AthleteModel>
                .Update.Set(x => x.Names, document.Names)
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

