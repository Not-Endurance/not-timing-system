using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application;
using Not.Application.HTTP;
using Not.Injection;
using Not.Krud.Abstractions;
using NTS.Application.Arrivelists;
using NTS.Application.Core;
using NTS.Application.PastEvents;
using NTS.Application.Presentlists;
using NTS.Application.Startlists;
using NTS.Contracts.Arrivelists;
using NTS.Contracts.Core;
using NTS.Contracts.PastEvents;
using NTS.Contracts.Presentlists;
using NTS.Contracts.Startlists;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application;

public static class NtsApplicationServices
{
    public static Builder ConfigureNtsApplication(
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
            _services.Add<IArrivelistService, ArrivelistService>(ServiceLifetime.Scoped);
            _services.Add<IPresentlistService, PresentlistService>(ServiceLifetime.Scoped);
            _services.Add<IStartUpcoming, IStartHistory, StartlistService>(ServiceLifetime.Scoped);
            return this;
        }

        public NApplicationBuilder ConfigureN()
        {
            return new NApplicationBuilder(_services, _configuration);
        }
    }
}
