using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class ClubMongoRepository : MongoRepository<ClubModel>
{
    readonly ITelemetryService _telemetry;

    public ClubMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.CLUBS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<ClubModel> GetUpdateDefinition(ClubModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(ClubMongoRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<ClubModel>.Update.Set(x => x.Name, document.Name);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
