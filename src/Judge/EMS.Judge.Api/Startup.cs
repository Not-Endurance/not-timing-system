using Core;
using Core.Application;
using Core.Application.Services;
using Core.Domain;
using Core.Localization;
using Core.Services;
using EMS.Judge.Api.Middlewares;
using EMS.Judge.Api.Services;
using EMS.Judge.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EMS.Judge.Api.Rpc;
using EMS.Judge.Api.Rpc.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using static Core.Application.CoreApplicationConstants;

namespace EMS.Judge.Api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        var assemblies = CoreConstants.Assemblies
            .Concat(LocalizationConstants.Assemblies)
            .Concat(DomainConstants.Assemblies)
            .Concat(CoreApplicationConstants.Assemblies)
            .Concat(ApplicationConstants.Assemblies)
            .Concat(ApiConstants.Assemblies)
            .ToArray();
        services
            .AddCore(assemblies)
            .AddApi();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IServiceProvider provider)
    {
        if (env.IsDevelopment())
        {
            app.UseMiddleware<ErrorLogger>();
        }
        else
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<StartlistHub>($"/{RpcEndpoints.STARTLIST}");
            endpoints.MapHub<WitnessEventsHub>($"/{RpcEndpoints.WITNESS_EVENTS}");
            endpoints.MapHub<ParticipantsHub>($"/{RpcEndpoints.PARTICIPANTS}");
            endpoints.MapHub<LoggingHub>($"/{RpcEndpoints.LOGGING}");
        });

        var broadcastService = provider.GetRequiredService<INetworkBroadcastService>();
        // TODO: is termination logic necessary. Does not seem so, but should be tested.
        Task.Run(() => new NetworkBroadcastService(broadcastService).StartAsync(new CancellationToken()));
        // attach event listeners that make RPCs
        provider.GetRequiredService<IEnumerable<IClientRpcService>>();
    }
}

public static class ApiServices
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services
            .AddSignalR(options => options.AddFilter<ErrorHandlerFilter>())
            .AddJsonProtocol(options => options.PayloadSerializerOptions.IncludeFields = true);
        services.AddControllers();
        services
            .AddTransient<ErrorLogger, ErrorLogger>()
            .AddTransient<IStartlistService, StartlistService>()
            .AddTransient<IWitnessEventService, WitnessEventService>();

        return services;
    }

    public static IServiceCollection AddInitializers(this IServiceCollection services, Assembly[] assemblies)
        => services
            .Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes =>
                    classes.AssignableTo<IInitializer>())
                .AsSelfWithInterfaces()
                .WithSingletonLifetime());
}
