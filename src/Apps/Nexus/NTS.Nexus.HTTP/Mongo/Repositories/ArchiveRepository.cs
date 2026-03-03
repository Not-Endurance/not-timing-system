using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Storage.Mongo;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class ArchiveRepository : MongoRepository<ArchiveEntryModel>, IArchiveRepository
{
    readonly ITelemetryService _telemetry;

    public ArchiveRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ARCHIVE_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<ArchiveEntryModel> GetUpdateDefinition(ArchiveEntryModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(ArchiveRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<ArchiveEntryModel>
                .Update.Set(x => x.Officials, document.Officials)
                .Set(x => x.Ranklists, document.Ranklists)
                .Set(x => x.EndDay, document.EndDay)
                .Set(x => x.StartDay, document.StartDay)
                .Set(x => x.City, document.City)
                .Set(x => x.Country, document.Country)
                .Set(x => x.Location, document.Location);
        }
        catch (Exception ex)
        {
            ex.AttachToCurrentActivity();
            throw;
        }
    }

    public async Task<IEnumerable<ArchiveEntryModel>> GetPerformances(int horseId)
    {
        using var activity = _telemetry.StartActivity(nameof(ArchiveRepository), nameof(GetPerformances));

        try
        {
            return await GetCollection()
                .Aggregate()
                .Match(x => x.Ranklists.Any(y => y.Entries.Any(z => z.Participation.Combination.Horse.Id == horseId)))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            ex.AttachToCurrentActivity();
            throw;
        }
    }
}

public interface IArchiveRepository : IRepository<ArchiveEntryModel>
{
    Task<IEnumerable<ArchiveEntryModel>> GetPerformances(int horseId);
}
