using Microsoft.Extensions.Configuration;
using Not.Application.Environments;

namespace Not.Application.Configurations;

public static class ConfigurationManagerExtensions
{
    public static void AddNAppsettings(this IConfigurationManager manager)
    {
        var environment = EnvironmentHelper.GetEnvironment();

        var config = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        manager.AddConfiguration(config);
    }
}
