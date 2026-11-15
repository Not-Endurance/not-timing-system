using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application;
using NC.Injection;
using NC.Storage.Mongo;

namespace NC.Storage;

public class NStorageBuilder
{
    readonly IServiceCollection _services;
    readonly NApplicationBuilder _nApplicationBuilder;

    public NStorageBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _nApplicationBuilder = new(services, configuration);
    }

    public NStorageBuilder AddMongoStorage(string connectionString, Assembly assembly)
    {
        MongoConventions.Register();

        _services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        _services.AddAsInterfaces(typeof(MongoRepository<>), ServiceLifetime.Transient, assembly);
        return this;
    }

    public NStorageBuilder AddRestApiStorage(Assembly _)
    {
        _nApplicationBuilder.AddHttp();
        return this;
    }
}
