namespace Not.Application.Environments;

public sealed class NEnvironment
{
    public NEnvironment(string? environment)
    {
        Environment = NEnvionmentNames.Normalize(environment);
    }

    public string Environment { get; }
}

public static class NEnvionmentNames
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
