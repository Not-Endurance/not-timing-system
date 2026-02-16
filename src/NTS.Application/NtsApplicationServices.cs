using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application;
using Not.Application.HTTP;
using Not.Application.RPC;
using Not.Injection;
using Not.Startup;
using NTS.Application.Startlists;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;

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
            _services.Add<IStartlistContext, StartlistService>(ServiceLifetime.Singleton);
            _services.Add<IStartUpcoming, IStartHistory, IStartupInitializer, StartlistService>(
                ServiceLifetime.Singleton
            );
            return this;
        }

        public NApplicationBuilder ConfigureN()
        {
            return new NApplicationBuilder(_services, _configuration);
        }
    }
}
