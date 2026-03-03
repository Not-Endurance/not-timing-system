using System.Diagnostics;
using MongoDB.Driver;
using Not.Application.Authentication.User;
using Not.Injection;
using Not.Storage.Mongo;
using NTS.Nexus.HTTP.Mongo.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserMongoRepository : IUserRepository, ITransient
{
    readonly IMongoCollection<NUserDocument> _collection;
    readonly ITelemetryService _telemetry;

    public UserMongoRepository(IMongoContext context, ITelemetryService telemetry)
    {
        _collection = context
            .Client.GetDatabase(MongoConstants.NTS_DATABASE)
            .GetCollection<NUserDocument>(MongoConstants.USERS_COLLECTION);
        _telemetry = telemetry;
    }

    public Task<NUserModel?> ReadByEmail(string email)
    {
        return Execute(
            nameof(ReadByEmail),
            async () =>
            {
                var normalizedEmail = NormalizeEmail(email);
                if (normalizedEmail == null)
                {
                    return null;
                }

                var user = await _collection.Find(x => x.Email == normalizedEmail).FirstOrDefaultAsync();
                return user?.ToUser();
            }
        );
    }

    public Task<NUserModel> Register(string email)
    {
        return Execute(
            nameof(Register),
            async () =>
            {
                var normalizedEmail =
                    NormalizeEmail(email) ?? throw new ArgumentException("Email cannot be empty", nameof(email));
                var existing = await ReadByEmail(normalizedEmail);
                if (existing != null)
                {
                    return existing;
                }

                var user = NUserDocument.Create(normalizedEmail);

                try
                {
                    await _collection.InsertOneAsync(user);
                    return user.ToUser();
                }
                catch (MongoWriteException ex) when (ex.WriteError.Code == 11000)
                {
                    var registeredUser = await ReadByEmail(normalizedEmail);
                    if (registeredUser != null)
                    {
                        return registeredUser;
                    }
                    throw new ApplicationException($"Could not register user '{normalizedEmail}'", ex);
                }
            }
        );
    }

    public Task Create(NUserModel item)
    {
        return Execute(
            nameof(Create),
            async () =>
            {
                var model = NUserDocument.From(item);
                model.Email = NormalizeEmail(model.Email) ?? throw new ApplicationException("Email cannot be empty");

                try
                {
                    await _collection.InsertOneAsync(model);
                }
                catch (MongoWriteException ex) when (ex.WriteError.Code == 11000)
                {
                    throw new ApplicationException(
                        $"Could not insert. Document with ID '{model.Id}' already exists",
                        ex
                    );
                }
            }
        );
    }

    public Task<IEnumerable<NUserModel>> ReadMany()
    {
        return Execute(
            nameof(ReadMany),
            async () =>
            {
                var users = await _collection.Find(_ => true).ToListAsync();
                return users.Select(x => x.ToUser()).ToArray().AsEnumerable();
            }
        );
    }

    static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Trim().ToLowerInvariant();
    }

    async Task Execute(string methodName, Func<Task> action)
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), methodName);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }

    async Task<T> Execute<T>(string methodName, Func<Task<T>> action)
    {
        using var activity = _telemetry.StartActivity(nameof(UserMongoRepository), methodName);

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}

public interface IUserRepository
{
    Task<NUserModel?> ReadByEmail(string email);
    Task<NUserModel> Register(string email);
}
