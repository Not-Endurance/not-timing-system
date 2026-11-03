using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.Configurations;
using Not.Application.DomainEvents;
using Not.Application.HTTP;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;

namespace Not.Application;

public class NApplicationBuilder
{
    readonly IServiceCollection _services;
    readonly IConfiguration _configuration;

    public NApplicationBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public NApplicationBuilder AddHttp(Action<NHttpSettings>? configure = null)
    {
        _services.AddHttpClient();
        _services.AddTransient<NHttpClient>();
        _services.AddSettings(_configuration, x => !string.IsNullOrWhiteSpace(x.Url), configure);
        return this;
    }

    public NApplicationBuilder AddRpcClient()
    {
        _services.AddScoped<IRpcSocket, SignalRSocket>();
        _services.AddSettings<RpcSettings>(
            _configuration,
            x => !string.IsNullOrWhiteSpace(x.Host) || !string.IsNullOrWhiteSpace(x.HubPattern)
        );
        return this;
    }

    public NApplicationBuilder AddDomainEvents()
    {
        _services.AddMediatR(config =>
            config
                .RegisterServicesFromAssembly(typeof(NApplicationBuilder).Assembly)
                .RegisterServicesFromAssembly(Assembly.GetCallingAssembly())
        );
        _services.AddTransient<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        return this;
    }

    public NApplicationBuilder AddUserSessions()
    {
        _services.AddScoped<INUserSession, NUserSessionService>();
        return this;
    }
}
