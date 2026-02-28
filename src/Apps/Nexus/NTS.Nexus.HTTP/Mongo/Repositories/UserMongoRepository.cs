using MongoDB.Driver;
using Not.Application.Authentication.User;
using Not.Injection;
using Not.Storage.Mongo;
using NTS.Nexus.HTTP.Mongo.Models;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserMongoRepository : IUserRepository, ITransient
{
    static readonly SemaphoreSlim INDEX_LOCK = new(1, 1);

    readonly IMongoCollection<NUserDocument> _collection;
    static bool _emailIndexInitialized;

    public UserMongoRepository(IMongoContext context)
    {
        _collection = context.Client.GetDatabase(MongoConstants.NTS_DATABASE).GetCollection<NUserDocument>(
            MongoConstants.USERS_COLLECTION
        );
    }

    public async Task<NUserModel?> ReadByEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null)
        {
            return null;
        }

        var user = await _collection.Find(x => x.Email == normalizedEmail).FirstOrDefaultAsync();
        return user?.ToUser();
    }

    public async Task<NUserModel> Register(string email)
    {
        var normalizedEmail = NormalizeEmail(email) ?? throw new ArgumentException("Email cannot be empty", nameof(email));
        var existing = await ReadByEmail(normalizedEmail);
        if (existing != null)
        {
            return existing;
        }

        var user = NUserDocument.Create(normalizedEmail);
        await EnsureEmailIndex();

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

    public async Task Create(NUserModel item)
    {
        var model = NUserDocument.From(item);
        model.Email = NormalizeEmail(model.Email) ?? throw new ApplicationException("Email cannot be empty");
        await EnsureEmailIndex();

        try
        {
            await _collection.InsertOneAsync(model);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == 11000)
        {
            throw new ApplicationException($"Could not insert. Document with ID '{model.Id}' already exists", ex);
        }
    }

    public async Task<IEnumerable<NUserModel>> ReadMany()
    {
        var users = await _collection.Find(_ => true).ToListAsync();
        return users.Select(x => x.ToUser()).ToArray();
    }

    static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Trim().ToLowerInvariant();
    }

    async Task EnsureEmailIndex()
    {
        if (_emailIndexInitialized)
        {
            return;
        }

        await INDEX_LOCK.WaitAsync();
        try
        {
            if (_emailIndexInitialized)
            {
                return;
            }

            var index = new CreateIndexModel<NUserDocument>(
                Builders<NUserDocument>.IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions { Unique = true, Name = "email_unique" }
            );
            await _collection.Indexes.CreateOneAsync(index);
            _emailIndexInitialized = true;
        }
        finally
        {
            INDEX_LOCK.Release();
        }
    }
}

public interface IUserRepository
{
    Task<NUserModel?> ReadByEmail(string email);
    Task<NUserModel> Register(string email);
}
