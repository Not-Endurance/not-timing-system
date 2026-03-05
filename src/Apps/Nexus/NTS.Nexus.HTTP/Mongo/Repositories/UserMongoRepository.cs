using MongoDB.Driver;
using Not.Application.Authentication.User;
using Not.Injection;
using Not.Storage.Mongo;
using NTS.Nexus.HTTP.Mongo.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserMongoRepository : IUserRepository, ITransient
{
    readonly IMongoContext _context;
    readonly ITelemetryService _telemetry;

    public UserMongoRepository(IMongoContext context, ITelemetryService telemetry)
    {
        _context = context;
        _telemetry = telemetry;
    }

    public async Task<NUserModel?> ReadByEmail(string email)
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), nameof(ReadByEmail));

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null)
        {
            return null;
        }

        var user = await GetCollection().Find(x => x.Email == normalizedEmail).FirstOrDefaultAsync();
        return user?.ToUser();
    }

    public async Task<NUserModel> Register(string email)
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), nameof(Register));

        var normalizedEmail = NormalizeEmail(email)
            ?? throw new ArgumentException("Email cannot be empty", nameof(email));
        var existing = await ReadByEmail(normalizedEmail);
        if (existing != null)
        {
            return existing;
        }

        var user = NUserDocument.Create(normalizedEmail);

        try
        {
            await GetCollection().InsertOneAsync(user);
            return user.ToUser();
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == 11000)
        {
            throw new ApplicationException($"Could not register user '{normalizedEmail}'", ex);
        }
    }

    public async Task Create(NUserModel item)
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), nameof(Create));

        var model = NUserDocument.From(item);
        model.Email = NormalizeEmail(model.Email) ?? throw new ApplicationException("Email cannot be empty");

        try
        {
            await GetCollection().InsertOneAsync(model);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == 11000)
        {
            throw new ApplicationException($"Could not insert. Document with ID '{model.Id}' already exists", ex);
        }
    }

    IMongoCollection<NUserDocument> GetCollection()
    {
        return _context.Client.GetDatabase(MongoConstants.NTS_DATABASE).GetCollection<NUserDocument>(MongoConstants.USERS_COLLECTION);
    }

    static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Trim().ToLowerInvariant();
    }
}

public interface IUserRepository
{
    Task<NUserModel?> ReadByEmail(string email);
    Task<NUserModel> Register(string email);
}
