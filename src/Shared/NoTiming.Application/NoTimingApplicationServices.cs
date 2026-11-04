using System.Reflection;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application;
using NC.Application.HTTP;
using NC.Injection;
using NC.Krud.Abstractions;
using NoTiming.Application.Arrivelists;
using NoTiming.Application.Contracts.Arrivelists;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.PastEvents;
using NoTiming.Application.Contracts.Presentlists;
using NoTiming.Application.Contracts.Startlists;
using NoTiming.Application.Core;
using NoTiming.Application.PastEvents;
using NoTiming.Application.Presentlists;
using NoTiming.Application.Startlists;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects.Payloads;

namespace NoTiming.Application;

public static class NoTimingApplicationServices
{
    public static Builder ConfigureNoTimingApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly rootAssembly
    )
    {
        services.AddHttpClient();
        services.AddTransient<NHttpClient>();
        services.AddNConventionalServices(rootAssembly);
        return new(services, configuration);
    }

    public class Builder
    {
        readonly IServiceCollection _services;
        readonly IConfiguration _configuration;

        internal Builder(IServiceCollection services, IConfiguration configuration)
        {
            _services = services;
            _configuration = configuration;
        }

        public Builder AddSharedCoreDomainServices()
        {
            _services.Add<IEventInformationService, IActiveEventsContext, EventInformationService>(
                ServiceLifetime.Scoped
            );
            _services.Add<IPastEventService, IPastEventContext, IKrudListBehind<EventInformation>, PastEventService>(
                ServiceLifetime.Scoped
            );
            _services.Add<
                IArrivelistService,
                INotificationHandler<ParticipationArrived>,
                INotificationHandler<PhaseCompleted>,
                INotificationHandler<ParticipationRestored>,
                INotificationHandler<ParticipationEliminated>,
                INotificationHandler<EventConnected>,
                ArrivelistService
            >(ServiceLifetime.Scoped);
            _services.AddScoped<INotificationHandler<EventDisconnected>>(x =>
                x.GetRequiredService<ArrivelistService>()
            );
            _services.Add<
                IPresentlistService,
                INotificationHandler<ParticipationArrived>,
                INotificationHandler<PhaseCompleted>,
                INotificationHandler<InspectionRequired>,
                INotificationHandler<RepresentationRequired>,
                INotificationHandler<ParticipationRestored>,
                INotificationHandler<ParticipationEliminated>,
                INotificationHandler<VetInAcknoledged>,
                INotificationHandler<EventConnected>,
                INotificationHandler<EventDisconnected>,
                PresentlistService
            >(ServiceLifetime.Scoped);
            _services.Add<
                IStartUpcoming,
                IStartHistory,
                INotificationHandler<PhaseCompleted>,
                INotificationHandler<ParticipationRestored>,
                INotificationHandler<ParticipationEliminated>,
                INotificationHandler<EventConnected>,
                StartlistService
            >(ServiceLifetime.Scoped);
            _services.AddScoped<INotificationHandler<EventDisconnected>>(x => x.GetRequiredService<StartlistService>());
            return this;
        }

        public NApplicationBuilder ConfigureN()
        {
            return new NApplicationBuilder(_services, _configuration);
        }
    }
}
