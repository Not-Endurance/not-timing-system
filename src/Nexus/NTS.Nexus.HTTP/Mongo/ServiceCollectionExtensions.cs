using Microsoft.Extensions.DependencyInjection;

namespace NTS.Nexus.HTTP.Mongo;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongo(this IServiceCollection services, string? connectionString)
    {
        services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        return services;
    }
}
