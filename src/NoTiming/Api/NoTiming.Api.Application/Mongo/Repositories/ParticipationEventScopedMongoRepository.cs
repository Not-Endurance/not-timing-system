using System.Diagnostics;
using MongoDB.Driver;
using NC.Server.ClientIdentity;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Features.Participations;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;

namespace NoTiming.Api.Application.Mongo.Repositories;

public class ParticipationEventScopedMongoRepository
    : EventScopedMongoRepository<ParticipationModel>,
        IParticipationChangeRepository
{
    readonly IRequestClientIdentity _clientIdentity;
    readonly ITelemetryService _telemetry;

    public ParticipationEventScopedMongoRepository(
        IMongoContext context,
        ITelemetryService telemetry,
        IRequestClientIdentity clientIdentity
    )
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.PARTICIPATIONS_COLLECTION)
    {
        _telemetry = telemetry;
        _clientIdentity = clientIdentity;
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
                .Update.Set(x => x.DomainVersion, document.DomainVersion)
                .Set(x => x.Category, document.Category)
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

    /// <summary>
    /// Saves a change that clients must hear about. The outbox marker is set in the same write, so the event bus
    /// announces the change and the writer is recorded for the client that made it.
    /// </summary>
    public async Task UpdateAndAnnounceChange(ParticipationModel document)
    {
        await EnsureEventNotFinalized(document.EventId);

        var update = Builders<ParticipationModel>.Update.Combine(
            GetUpdateDefinition(document),
            Builders<ParticipationModel>.Update.Set(x => x.Emitted, false).Set(x => x.WriterId, _clientIdentity.ClientId)
        );
        await GetCollection().UpdateOneAsync(GetItemFilter(document), update);
    }
}
