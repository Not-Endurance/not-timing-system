using System.Security.Authentication;
using MongoDB.Driver;

namespace NTS.Nexus.HTTP.Mongo;

public class MongoContext : IMongoContext
{
    public MongoContext(string? connectionString)
    {
        var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
        settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
        Client = new MongoClient(settings);
    }

    public MongoClient Client { get; }
}

public interface IMongoContext
{
    MongoClient Client { get; }
}
