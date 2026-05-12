using System.Reflection;

namespace Not.Application.Environments;

public static class EnvironmentHelper
{
    const string IS_LOCALHOST_VARIABLE = "IS_LOCALHOST";
    public const string ENVIRONMENT_VARIABLE = "ASPNETCORE_ENVIRONMENT";
    public const string DEVELOPMENT = NEnvionmentNames.DEVELOPMENT;
    public const string STAGING = NEnvionmentNames.STAGING;
    public const string PRODUCTION = NEnvionmentNames.PRODUCTION;
    public const string TARGET_ENVIRONMENT_METADATA = "NtsTargetEnvironment";

    public static bool Is(string env)
    {
        return Environment.GetEnvironmentVariable(env) != null;
    }

    public static string GetEnvironment(Assembly? assembly = null)
    {
        var environment = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE);
        if (string.IsNullOrWhiteSpace(environment) && assembly != null)
        {
            environment = GetAssemblyTargetEnvironment(assembly);
        }

        return Normalize(environment);
    }

    public static string UseResolvedEnvironment(Assembly? assembly = null)
    {
        var environment = GetEnvironment(assembly);
        Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE, environment);
        return environment;
    }

    public static string Normalize(string? environment)
    {
        return NEnvionmentNames.Normalize(environment);
    }

    public static bool IsLocalhost()
    {
        var isLocalhost = Environment.GetEnvironmentVariable(IS_LOCALHOST_VARIABLE);
        return !string.IsNullOrEmpty(isLocalhost);
    }

    static string? GetAssemblyTargetEnvironment(Assembly assembly)
    {
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                attribute.Key.Equals(TARGET_ENVIRONMENT_METADATA, StringComparison.OrdinalIgnoreCase)
            )
            ?.Value;
    }
}
