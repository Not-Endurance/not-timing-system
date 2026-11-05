using MongoDB.Driver;
using NC.Application.Authentication.User;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Mongo.Models;
using NoTiming.Api.Application.Telemetry;

namespace NoTiming.Api.Application.Mongo.Repositories;

public class UserMongoRepository : IUserRepository
{
    readonly IMongoContext _context;
    readonly ITelemetryService _telemetry;

    public UserMongoRepository(IMongoContext context, ITelemetryService telemetry)
    {
        _context = context;
        _telemetry = telemetry;
    }

    public async Task<IEnumerable<NUserModel>> ReadMany()
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), nameof(ReadMany));

        var users = await GetCollection().Find(_ => true).ToListAsync();
        return users.Select(x => x.ToUser()).ToArray();
    }

    IMongoCollection<NUserDocument> GetCollection()
    {
        return _context
            .Client.GetDatabase(MongoConstants.NTS_DATABASE)
            .GetCollection<NUserDocument>(MongoConstants.USERS_COLLECTION);
    }
}

public interface IUserRepository
{
    Task<IEnumerable<NUserModel>> ReadMany();
}
