using System.Security.Authentication;
using MongoDB.Driver;
using Not.Injection;

namespace NTS.Nexus.HTTP.Mongo;

public class MongoContext : IMongoContext
{
    public MongoContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
        settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
        Client = new MongoClient(settings);
    }

    public MongoClient Client { get; }
}

public interface IMongoContext : ISingleton
{
    MongoClient Client { get; }
}
