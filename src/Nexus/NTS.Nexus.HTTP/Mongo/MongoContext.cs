using System.Security.Authentication;
using MongoDB.Driver;
using Not.Injection;

namespace NTS.Nexus.HTTP.Mongo;

public class MongoContext : IMongoContext
{
    public MongoContext()
    {
        var connectionString = @"mongodb://nts-mongo-dev:t4aX66O4VMIvO4vnLvMUEP3sVt8tfcAM651094Xl1WRzv1VsQY9qI48RTb7elIW7kEIt8AcJHfLPACDbrAqJEg==@nts-mongo-dev.mongo.cosmos.azure.com:10255/?ssl=true&retrywrites=false&replicaSet=globaldb&maxIdleTimeMS=120000&appName=@nts-mongo-dev@";
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
