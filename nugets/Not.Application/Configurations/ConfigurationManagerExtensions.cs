using System.Reflection;
using Microsoft.Extensions.Configuration;
using Not.Application.Environments;
using Not.Exceptions;

namespace Not.Application.Configurations;

public static class ConfigurationManagerExtensions
{
    /// <summary>
    /// Embeds appsettings.json and appsettins.{env}.json and environment based user-secrets for localhost
    /// </summary>
    /// <param name="manager">Configuration manager</param>
    /// <param name="assembly">Assembly to embed settings in</param>
    /// <param name="prefix">Application prefix used to identify app-specific environment secrets in localhost</param>
    public static void AddNAppsettings(this IConfigurationManager manager, Assembly assembly, string prefix)
    {
        var environment = EnvironmentHelper.GetEnvironment();

        var builder = new ConfigurationBuilder()
            .AddEmbeddedJsonFile("appsettings.json", optional: false, assembly)
            .AddEmbeddedJsonFile($"appsettings.{environment}.json", optional: true, assembly);
        if (EnvironmentHelper.IsLocalhost())
        {
            builder.AddSecrets($"{prefix}-{environment}");
        }
        var config = builder.Build();

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

    public static IConfigurationBuilder AddSecrets(this IConfigurationBuilder builder, string secretId)
    {
        GuardHelper.ThrowIfDefault(secretId);

        builder.AddUserSecrets(secretId.ToLower());
        return builder;
    }
}
