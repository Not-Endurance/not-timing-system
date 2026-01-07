using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Filesystem;
using Not.Storage;
using NTS.Judge.Features.Core;
using NTS.Storage.Core;
using NTS.Storage.Core.Repositories;
using NTS.Storage.JSON;
using NTS.Storage.REST;

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

        public Builder AddCoreJsonStorage()
        {

            _nStorageBuilder.AddJsonFileStorage<CoreState, CoreJsonStore, ICoreState>();
            
            // TODO: extract conventional logic to apply to IRepository<T> and other interfaces directly instead of listing manually
            _services
                .AddTransient<IRepository<Domain.Core.Aggregates.EnduranceEvent>, EnduranceEventRepository>()
                .AddTransient<IRepository<Domain.Core.Aggregates.Handout>, HandoutRepository>()
                .AddTransient<IRepository<Domain.Core.Aggregates.Official>, OfficialRepository>()
                .AddTransient<IRepository<Domain.Core.Aggregates.Participation>, ParticipationRepository>()
                .AddTransient<IRepository<Domain.Core.Aggregates.Ranking>, RankingRepository>()
                .AddTransient<IRepository<Domain.Core.Aggregates.SnapshotResult>, SnapshotResultRepository>();
            return this;
        }

        public Builder AddMongoStorage(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ApplicationException("MongoDB connection string is null");
            }
            _nStorageBuilder.AddMongoStorage(connectionString);
            return this;
        }

        public Builder AddRestApiStorage()
        {
            _nStorageBuilder.AddRestApiStorage();
            _services
                .AddTransient<IRepository<Domain.Setup.Aggregates.Athlete>, AthleteRestApiRepository>()
                .AddTransient<IRepository<Domain.Setup.Aggregates.Club>, ClubRestApiRepository>()
                .AddTransient<IRepository<Domain.Setup.Aggregates.Horse>, HorseRestApiRepository>()
                .AddTransient<IRepository<Domain.Setup.Aggregates.UpcomingEvent>, UpcomingEventRestApiRepository>()
                .AddTransient<IRepository<Domain.Aggregates.Country>, CountryRestApiRepository>()
                .AddTransient<ISettingRepository, SettingHttpRepository>()
                .AddTransient<IRepository<Domain.Core.Aggregates.ArchiveEntry>, ArchiveRestApiRepository>();
            return this;
        }
    }
}
