using Microsoft.Extensions.DependencyInjection;

namespace NTS.Nexus.HTTP.Mongo;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongo(
        this IServiceCollection services,
        string? connectionString
    )
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ApplicationException(
                "Mongo is not configured - connectionString is required"
            );
        }
        services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        return services;
    }
}
