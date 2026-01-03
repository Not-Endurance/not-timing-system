using System.Reflection;
using Microsoft.Extensions.Configuration;
using Not.Application.Environments;

namespace Not.Application.Configurations;

public static class ConfigurationManagerExtensions
{
    public static void AddNAppsettings(this IConfigurationManager manager, Assembly assembly)
    {
        var environment = EnvironmentHelper.GetEnvironment();

        var config = new ConfigurationBuilder()
            .AddEmbeddedJsonFile("appsettings.json", optional: false, assembly)
            .AddEmbeddedJsonFile($"appsettings.{environment}.json", optional: true, assembly)
            .AddSecrets(environment, assembly)
            .Build();

        manager.AddConfiguration(config);
    }

    public static IConfigurationBuilder AddEmbeddedJsonFile(
        this IConfigurationBuilder builder,
        string fileName,
        bool optional = false,
        Assembly? assembly = null
    )
    {
        assembly ??= Assembly.GetExecutingAssembly();

        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            if (optional)
            {
                return builder;
            }

            throw new FileNotFoundException(
                $"Embedded resource '{fileName}' not found in assembly '{assembly.FullName}'."
            );
        }

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            if (optional)
            {
                return builder;
            }

            throw new InvalidOperationException($"Could not load stream for embedded resource '{resourceName}'.");
        }

        return builder.AddJsonStream(stream);
    }

    public static IConfigurationBuilder AddSecrets(
    this IConfigurationBuilder builder,
    string environment,
    Assembly? assembly = null
)
    {
        assembly ??= Assembly.GetExecutingAssembly();
#if DEBUG
        return builder.AddUserSecrets(assembly);
#else
        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return builder.AddUserSecrets(assembly);
        }
        return builder.AddEnvironmentVariables();
#endif
    }
}
