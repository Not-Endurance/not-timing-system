using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Injection;
using Not.Storage;
using Not.Storage.REST;
using NTS.Judge.Features.Core;
using NTS.Storage.Core;
using NTS.Storage.JSON;

namespace NTS.Storage;

public static class NtsStorageServices
{
    public static Builder ConfigureNtsStorage(this IServiceCollection services, IConfiguration configuration)
    {
        return new(services, configuration);
    }

    public class Builder
    {
        readonly NStorageBuilder _nStorageBuilder;
        readonly IServiceCollection _services;

        internal Builder(IServiceCollection services, IConfiguration configuration)
        {
            _nStorageBuilder = new(services, configuration);
            _services = services;
        }

        public Builder AddJsonStorage()
        {
            _nStorageBuilder.AddJsonFileStorage<CoreState, CoreJsonStore, ICoreState>(Assembly.GetExecutingAssembly());
            _services.AddAsInterfaces<SocketConnectionHookStorage>(ServiceLifetime.Singleton);
            return this;
        }

        public Builder AddRestApiStorage()
        {
            _nStorageBuilder.AddRestApiStorage(Assembly.GetExecutingAssembly());
            return this;
        }
    }
}
