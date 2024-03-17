using Core.Application;
using Core.Domain;
using Core.Localization;
using Core;
using EMS.Judge.Api.Configuration;
using EMS.Judge.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using Core.Application.Services;
using EMS.Judge.Application.Services;
using Core.Services;
using System.Reflection;

namespace EMS.Judge.Api;

public static class Program
{
    public static void StartEmbedded(IServiceProvider appServiceProvider)
    {
        StartHost(services => services.AddSingleton<IJudgeServiceProvider>(new JudgeServiceProvider(appServiceProvider)));
    }

    public static void Main()
    {
        StartHost(services =>
        {
            var assemblies = CoreConstants.Assemblies
                .Concat(LocalizationConstants.Assemblies)
                .Concat(DomainConstants.Assemblies)
                .Concat(CoreApplicationConstants.Assemblies)
                .Concat(ApplicationConstants.Assemblies)
                .Concat(ApiConstants.Assemblies)
                .ToArray();

            var judgeServices = new ServiceCollection()
                .AddCore(assemblies)
                .AddDomain(assemblies)
                .AddCoreApplication(assemblies)
                .AddJudgeApplication(assemblies);

#pragma warning disable ASP0000 // Used to simulate how services work in StartEmbedded
            services
                .AddTransient<INotificationService, NotificationMock>()
                .AddSingleton<ISettings, SettingsMock>()
                .AddSingleton<IJudgeServiceProvider>(new JudgeServiceProvider(judgeServices.BuildServiceProvider()))
                .AddCore(assemblies)
                .AddDomain(assemblies)
                .AddCoreApplication(assemblies)
                .AddJudgeApplication(assemblies)
                .AddInitializers(assemblies);
#pragma warning restore ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
        });
    }

    private static void StartHost(Action<IServiceCollection> configureServices)
    {
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
                webBuilder
                    .ConfigureServices(configureServices)
                    .UseUrls("http://*:11337")
                    .UseStartup<Startup>())
            .Build()
            .Run();
    }
}

public class NotificationMock : INotificationService
{
    public void Error(string message)
    {
    }
}

public class SettingsMock : ISettings
{
    public bool IsConfigured { get; }

    public bool IsSandboxMode => false;

    public bool StartServer => false;

    public bool StartVupRfid => false;

    public bool UseVD67InManager => false;

    public string Version => "test";

    public string WitnessEventType { get; set; } = "Test";
}
