using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Storage;
using Not.Storage.REST;
using NTS.Application.PastEvents;
using NTS.Storage.Core.Repositories;

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
        readonly IServiceCollection _services;
        readonly NStorageBuilder _nStorageBuilder;

        internal Builder(IServiceCollection services, IConfiguration configuration)
        {
            _services = services;
            // The keyed filesystem context is still used for FEI export output even though JSON file storage is gone.
            var factory = FileContextHelper.CreateFileContextFactory("stores");
            services.AddKeyedSingleton<IFilesystemContext, FilesystemContext>(DATA_KEY, factory);
            _nStorageBuilder = new(services, configuration);
        }

        public Builder AddRestApiStorage()
        {
            _nStorageBuilder.AddRestApiStorage(Assembly.GetExecutingAssembly());
            _services.AddTransient<IPastParticipationRepository, PastParticipationRepository>();
            _services.AddTransient<IPastRankingRepository, PastRankingRepository>();
            _services.AddTransient<IPastOfficialRepository, PastOfficialRepository>();
            return this;
        }
    }
}
