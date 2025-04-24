namespace Not.Application.Environments;

public static class EnvironmentHelper
{
    const string ENVIRONMENT_VARIABLE = "ASPNETCORE_ENVIRONMENT";
    public const string LOCALHOST = "Development";
    public const string STAGING = "Staging";
    public const string PRODUCTION = "Production";

    public static bool IsLocalhost()
    {
        return Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE) == LOCALHOST;
    }

    public static bool Is(string env)
    {
        return Environment.GetEnvironmentVariable(env) == null;
    }

    public static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE) ?? "Staging"; // TODO: figure out how to pass this
    }
}
