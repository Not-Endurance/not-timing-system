namespace Not.Application.Environments;

public static class EnvironmentHelper
{
    const string ENVIRONMENT_VARIABLE = "ASPNETCORE_ENVIRONMENT";
    const string IS_LOCALHOST_VARIABLE = "IS_LOCALHOST";
    public const string DEVELOPMENT = "Development";
    public const string LOCALHOST = "Localhost";

    public static bool Is(string env)
    {
        return Environment.GetEnvironmentVariable(env) != null;
    }

    public static string GetEnvironment()
    {
        var aspnetEnvironment = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE) ?? "Staging"; // TODO: figure out how to pass this
        return MapNEnvironment(aspnetEnvironment);
    }

    public static bool IsLocalhost()
    {
        var isLocalhost = Environment.GetEnvironmentVariable(IS_LOCALHOST_VARIABLE);
        return !string.IsNullOrEmpty(isLocalhost);
    }

    static string MapNEnvironment(string aspEnvironment)
    {
        if (aspEnvironment == DEVELOPMENT)
        {
            return LOCALHOST;
        }
        return aspEnvironment;
    }
}
