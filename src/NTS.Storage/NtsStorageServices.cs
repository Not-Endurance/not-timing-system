using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Filesystem;
using Not.Storage;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.Core.Repositories;

namespace NTS.Storage;

public static class NtsStorageServices
{
    // Necessary to be called directly from UI project, otherwise the runtime treeshakes this
    // DLL off, because no resources are explicitly referenced.
    public static Builder ConfigureNtsStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        string debugRootDirectoryName = "nts"
    )
    {
        FileContextHelper.SetDebugRootDirectory(debugRootDirectoryName);
        return new(services, configuration);
    }

    public class Builder
    {
        readonly IServiceCollection _services;
        readonly NStorageBuilder _nStorageBuilder;

        internal Builder(IServiceCollection services, IConfiguration configuration)
        {
            _services = services;
            _nStorageBuilder = new(services, configuration);
        }

        public void AddCoreJsonStorage()
        {

            _nStorageBuilder.AddJsonFile();
            
            // TODO: extract conventional logic to apply to IRepository<T> and other interfaces directly instead of listing manually
            _services
                .AddTransient<IRepository<EnduranceEvent>, EnduranceEventRepository>()
                .AddTransient<IRepository<Handout>, HandoutRepository>()
                .AddTransient<IRepository<Official>, OfficialRepository>()
                .AddTransient<IRepository<Participation>, ParticipationRepository>()
                .AddTransient<IRepository<Ranking>, RankingRepository>()
                .AddTransient<IRepository<SnapshotResult>, SnapshotResultRepository>();
        }

        public void AddMongoStorage(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ApplicationException("MongoDB connection string is null");
            }
            _nStorageBuilder.AddMongo(connectionString);
        }
    }
}
