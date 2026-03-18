using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Storage;
using Not.Storage.JsonFile.Stores;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Storage.REST;

namespace NTS.Storage;

public static class NtsStorageServices
{
    const string DATA_KEY = "NDataKey";

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
            var factory = FileContextHelper.CreateFileContextFactory("stores");
            _services.AddKeyedSingleton<IFilesystemContext, FilesystemContext>(DATA_KEY, factory);
            return this;
        }

        public Builder AddRestApiStorage()
        {
            _nStorageBuilder.AddRestApiStorage(Assembly.GetExecutingAssembly());
            _services.AddSingleton<ICoreState, RestApiCoreState>();
            return this;
        }
    }
}
