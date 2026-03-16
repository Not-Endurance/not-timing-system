using System.Reflection;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application;
using Not.Injection;
using NTS.Application.Startlists;
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

        public Builder AddStartlist()
        {
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
