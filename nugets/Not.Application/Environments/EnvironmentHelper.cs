namespace Not.Application.Environments;

public static class EnvironmentHelper
{
    const string ENVIRONMENT_VARIABLE = "ASPNETCORE_ENVIRONMENT";
    const string IS_LOCALHOST_VARIABLE = "IS_LOCALHOST";
    public const string DEVELOPMENT = "Development";

    public static bool Is(string env)
    {
        return Environment.GetEnvironmentVariable(env) != null;
    }

    public static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE) ?? "Staging"; // TODO: figure out how to pass this
    }

    public static bool IsLocalhost()
    {
        var isLocalhost = Environment.GetEnvironmentVariable(IS_LOCALHOST_VARIABLE);
        return !string.IsNullOrEmpty(isLocalhost);
    }
}
