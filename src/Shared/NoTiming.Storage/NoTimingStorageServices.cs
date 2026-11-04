using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application.Authentication.Abstractions;
using NC.Application.CRUD.Ports;
using NC.Storage;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Application.Core;
using NoTiming.Application.Settings;
using NoTiming.Application.Setup;
using NoTiming.Application.UserSession;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Storage.Core.Repositories;
using NoTiming.Storage.REST;

namespace NoTiming.Storage;

public static class NoTimingStorageServices
{
    public static Builder ConfigureNoTimingStorage(this IServiceCollection services, IConfiguration configuration)
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
            _nStorageBuilder = new(services, configuration);
        }

        public Builder AddRestApiStorage()
        {
            _nStorageBuilder.AddRestApiStorage(Assembly.GetExecutingAssembly());
            _services.AddTransient(typeof(EventScopeFactory<>));

            _services.AddTransient<IEventInformationRepository, EventInformationApiRepository>();
            _services.AddTransient<ISettingRepository, SettingApiRepository>();
            _services.AddTransient<INoTimingUserSessionRepository, UserSessionApiRepository>();
            _services.AddTransient<INUserSessionRepository<NoTimingUserSessionStateModel>, UserSessionApiRepository>();
            _services.AddTransient<IRepository<Participation>, ParticipationApiRepository>();
            _services.AddTransient<IRepository<Ranking>, RankingApiRepository>();
            _services.AddTransient<IRepository<Official>, OfficialApiRepository>();
            _services.AddTransient<IRepository<Operator>, OperatorApiRepository>();
            _services.AddTransient<IRepository<Handout>, HandoutApiRepository>();
            _services.AddTransient<IRepository<Country>, CountryApiRepository>();
            _services.AddTransient<IRepository<Club>, ClubApiRepository>();
            _services.AddTransient<IRepository<Horse>, HorseApiRepository>();
            _services.AddTransient<IRepository<Athlete>, AthleteApiRepository>();
            _services.AddTransient<IRepository<ConfigureEvent>, ConfigureEventApiRepository>();
            _services.AddTransient<IUserLookup, UserApiRepository>();

            AddEventScopedRepository<Participation, ParticipationEventScopedApiRepository>();
            AddEventScopedRepository<Ranking, RankingEventScopedApiRepository>();
            AddEventScopedRepository<Official, OfficialEventScopedApiRepository>();
            AddEventScopedRepository<Operator, OperatorEventScopedApiRepository>();
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
