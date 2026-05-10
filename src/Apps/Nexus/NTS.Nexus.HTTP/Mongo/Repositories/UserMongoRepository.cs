using MongoDB.Driver;
using Not.Application.Authentication.User;
using Not.Storage.Mongo;
using NTS.Nexus.HTTP.Mongo.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserMongoRepository : IUserRepository
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

    public async Task<IEnumerable<NUserModel>> ReadMany()
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), nameof(ReadMany));

        var users = await GetCollection().Find(_ => true).ToListAsync();
        return users.Select(x => x.ToUser()).ToArray();
    }

    public async Task<NUserModel> Register(NUserRegistration registration)
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), nameof(Register));

        var normalizedEmail =
            NormalizeEmail(registration.Email)
            ?? throw new ArgumentException("Email cannot be empty", nameof(registration));
        var existing = await ReadByEmail(normalizedEmail);
        if (existing != null)
        {
            return existing;
        }

        var user = NUserDocument.Create(
            normalizedEmail,
            registration.Name,
            registration.GivenName,
            registration.Surname,
            registration.CountryRegion,
            registration.MiddleName,
            registration.Club,
            registration.FeiId
        );

        try
        {
            await GetCollection().InsertOneAsync(user);
            return user.ToUser();
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == 11000)
        {
            existing = await ReadByEmail(normalizedEmail);
            if (existing != null)
            {
                return existing;
            }

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
        return _context
            .Client.GetDatabase(MongoConstants.NTS_DATABASE)
            .GetCollection<NUserDocument>(MongoConstants.USERS_COLLECTION);
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
    Task<IEnumerable<NUserModel>> ReadMany();
    Task<NUserModel> Register(NUserRegistration registration);
}
