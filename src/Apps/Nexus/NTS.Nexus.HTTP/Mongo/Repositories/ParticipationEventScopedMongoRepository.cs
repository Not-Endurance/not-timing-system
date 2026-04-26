using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class ParticipationEventScopedMongoRepository : EventScopedMongoRepository<ParticipationModel>
{
    readonly ITelemetryService _telemetry;

    public ParticipationEventScopedMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.PARTICIPATIONS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<ParticipationModel> GetUpdateDefinition(ParticipationModel document)
    {
        using var activity = _telemetry.StartActivity(
            nameof(ParticipationEventScopedMongoRepository),
            nameof(GetUpdateDefinition)
        );

        try
        {
            return Builders<ParticipationModel>
                .Update.Set(x => x.Category, document.Category)
                .Set(x => x.Competition, document.Competition)
                .Set(x => x.Combination, document.Combination)
                .Set(x => x.Phases, document.Phases)
                .Set(x => x.Total, document.Total)
                .Set(x => x.EventId, document.EventId)
                .Set(x => x.Eliminated, document.Eliminated);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
