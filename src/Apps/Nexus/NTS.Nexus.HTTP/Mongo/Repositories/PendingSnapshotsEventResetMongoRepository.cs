using MongoDB.Driver;
using Not.Injection;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Watcher.Models;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class PendingSnapshotsEventResetMongoRepository : IEventResetRepository, ITransient
{
    readonly IMongoContext _context;

    public PendingSnapshotsEventResetMongoRepository(IMongoContext context)
    {
        _context = context;
    }

    public Task DeleteAllForEvent(int eventId)
    {
        return _context
            .Client.GetDatabase(MongoConstants.NTS_DATABASE)
            .GetCollection<PendingSnapshotsModel>(MongoConstants.PENDING_SNAPSHOTS_COLLECTION)
            .DeleteManyAsync(x => x.EventId == eventId);
    }
}
