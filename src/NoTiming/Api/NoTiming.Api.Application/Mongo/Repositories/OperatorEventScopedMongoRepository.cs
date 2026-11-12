using System.Diagnostics;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Core.Models;

namespace NoTiming.Api.Application.Mongo.Repositories;

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
            .Update.Set(x => x.DomainVersion, document.DomainVersion)
                .Set(x => x.EventId, document.EventId)
            .Set(x => x.UserId, document.UserId)
            .Set(x => x.Role, document.Role);
    }
}
