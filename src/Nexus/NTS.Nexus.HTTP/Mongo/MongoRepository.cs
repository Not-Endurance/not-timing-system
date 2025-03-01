using System.Linq.Expressions;
using System.Security.Authentication;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Domain;
using NTS.Storage.Documents;

namespace NTS.Nexus.HTTP.Mongo;

public abstract class MongoRepository<T> : IRepository<T>
    where T : Document, IAggregateRoot
{
    public MongoRepository(string db, string collection)
    {
        var connectionString =
            @"mongodb://nts-mongo-dev:t4aX66O4VMIvO4vnLvMUEP3sVt8tfcAM651094Xl1WRzv1VsQY9qI48RTb7elIW7kEIt8AcJHfLPACDbrAqJEg==@nts-mongo-dev.mongo.cosmos.azure.com:10255/?ssl=true&retrywrites=false&replicaSet=globaldb&maxIdleTimeMS=120000&appName=@nts-mongo-dev@";
        var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
        settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
        Collection = new MongoClient(settings).GetDatabase(db).GetCollection<T>(collection);
    }

    protected abstract UpdateDefinition<T> GetUpdateDefinition(T document);

    protected IMongoCollection<T> Collection { get; }

    public async Task Create(T document)
    {
        try
        {
            await Collection.InsertOneAsync(document);
        }
        catch (MongoWriteException ex)
        {
            if (ex.WriteError.Code == 11000)
            {
                throw new Exception(
                    $"Could not insert. Document with ID '{document.Id}' already exists",
                    ex
                ); //TODO: streamline validation with Middleware
            }
            else
            {
                throw;
            }
        }
    }

    public async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<T?> Read(int id)
    {
        return await Read(x => x.Id == id);
    }

    public async Task<IEnumerable<T>> ReadAll()
    {
        return await ReadAll(x => true);
    }

    public async Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter)
    {
        return await Collection.Find(filter).ToListAsync();
    }

    public async Task Update(T document)
    {
        var updateDefinition = GetUpdateDefinition(document);
        await Collection.UpdateOneAsync(x => x.Id == document.Id, updateDefinition);
    }

    public async Task Delete(int id)
    {
        await Collection.DeleteOneAsync(x => x.Id == id);
    }

    public Task Delete(T entity)
    {
        return Delete(entity.Id);
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        await Collection.DeleteManyAsync(filter);
    }

    public Task Delete(IEnumerable<T> entities)
    {
        throw new NotImplementedException(
            "Batch delete with full entities isn't supported. Probably remove this method"
        );
    }
}
