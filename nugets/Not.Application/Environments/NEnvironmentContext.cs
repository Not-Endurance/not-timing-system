namespace Not.Application.Environments;

public sealed class NEnvironmentContext : IEnvironmentContext
{
    public NEnvironmentContext(string? environment)
    {
        Environment = NEnvironmentNames.Normalize(environment);
    }

    public string Environment { get; }

    public bool IsProduction()
    {
        return Environment == NEnvironmentNames.PRODUCTION;
    }
}

public interface IEnvironmentContext
{
    string Environment { get; }
    bool IsProduction();
}

public static class NEnvironmentNames
{
    public const string DEVELOPMENT = "Development";
    public const string STAGING = "Staging";
    public const string PRODUCTION = "Production";

    public static string Normalize(string? environment)
    {
        if (string.IsNullOrWhiteSpace(environment))
        {
            return PRODUCTION;
        }

        var trimmed = environment.Trim();
        if (trimmed.Equals(DEVELOPMENT, StringComparison.OrdinalIgnoreCase))
        {
            return DEVELOPMENT;
        }

        if (trimmed.Equals(STAGING, StringComparison.OrdinalIgnoreCase))
        {
            return STAGING;
        }

        if (trimmed.Equals(PRODUCTION, StringComparison.OrdinalIgnoreCase))
        {
            return PRODUCTION;
        }

        return trimmed;
    }
}
