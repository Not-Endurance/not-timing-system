using System.Reflection;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application;
using Not.Injection;
using Not.Krud.Abstractions;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.PastEvents;
using NTS.Application.Contracts.Startlists;
using NTS.Application.Core;
using NTS.Application.PastEvents;
using NTS.Application.Startlists;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Application;

public static class NtsApplicationServices
{
    public static Builder ConfigureNtsApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly rootAssembly
    )
    {
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
            _services.Add<IEnduranceEventService, IActiveEventsContext, EnduranceEventService>(ServiceLifetime.Scoped);
            _services.Add<
                IPastEventService,
                IPastEventContext,
                IKrudListBehind<EnduranceEvent>,
                PastEventService
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
            _services.AddSingleton<INotificationHandler<EventDisconnected>>(x =>
                x.GetRequiredService<StartlistService>()
            );
            return this;
        }

        public NApplicationBuilder ConfigureN()
        {
            return new NApplicationBuilder(_services, _configuration);
        }
    }
}
