using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;
using Not.Application.CRUD.Ports;
using Not.Filesystem;
using Not.Storage;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.Core;
using NTS.Application.Settings;
using NTS.Application.Setup;
using NTS.Application.UserSession;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Storage.Core.Repositories;
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
            _services.AddTransient(typeof(EventScopeFactory<>));

            _services.AddTransient<IEventInformationRepository, EventInformationApiRepository>();
            _services.AddTransient<ISettingRepository, SettingApiRepository>();
            _services.AddTransient<INtsUserSessionRepository, UserSessionApiRepository>();
            _services.AddTransient<INUserSessionRepository<NtsUserSessionStateModel>, UserSessionApiRepository>();
            _services.AddTransient<IRepository<Participation>, ParticipationApiRepository>();
            _services.AddTransient<IRepository<Ranking>, RankingApiRepository>();
            _services.AddTransient<IRepository<Official>, OfficialApiRepository>();
            _services.AddTransient<IRepository<Country>, CountryApiRepository>();
            _services.AddTransient<IRepository<Club>, ClubApiRepository>();
            _services.AddTransient<IRepository<Horse>, HorseApiRepository>();
            _services.AddTransient<IRepository<Athlete>, AthleteApiRepository>();
            _services.AddTransient<IRepository<ConfigureEvent>, ConfigureEventApiRepository>();
            _services.AddTransient<IUserEmailLookup, UserApiRepository>();

            AddEventScopedRepository<Participation, ParticipationEventScopedApiRepository>();
            AddEventScopedRepository<Ranking, RankingEventScopedApiRepository>();
            AddEventScopedRepository<Official, OfficialEventScopedApiRepository>();
            AddEventScopedRepository<Handout, HandoutEventScopedApiRepository>();
            AddEventScopedRepository<SnapshotResult, SnapshotResultEventScopedApiRepository>();
            return this;
        }

        void AddEventScopedRepository<T, TImplementation>()
            where TImplementation : class, IEventScopedRepository<T>
        {
            _services.AddTransient<IEventScopedRepository<T>, TImplementation>();
        }
    }
}
