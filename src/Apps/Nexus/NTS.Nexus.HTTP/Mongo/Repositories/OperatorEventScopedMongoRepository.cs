using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class OperatorEventScopedMongoRepository : EventScopedMongoRepository<OperatorModel>
{
    readonly ITelemetryService _telemetry;

    public OperatorEventScopedMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.OPERATORS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<OperatorModel> GetUpdateDefinition(OperatorModel document)
    {
        return Builders<OperatorModel>
            .Update.Set(x => x.EventId, document.EventId)
            .Set(x => x.UserId, document.UserId)
            .Set(x => x.Role, document.Role);
    }
}
