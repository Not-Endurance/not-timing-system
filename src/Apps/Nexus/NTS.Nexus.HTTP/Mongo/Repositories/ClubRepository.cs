using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Setup;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class ClubRepository : MongoRepository<ClubModel>
{
    readonly ITelemetryService _telemetry;

    public ClubRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.CLUBS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<ClubModel> GetUpdateDefinition(ClubModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(ClubRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<ClubModel>.Update.Set(x => x.Name, document.Name);
        }
        catch (Exception ex)
        {
            ex.AttachToCurrentActivity();
            throw;
        }
    }
}
