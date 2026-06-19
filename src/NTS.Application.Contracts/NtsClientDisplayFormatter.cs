using Not.Application.Environments;

namespace NTS.Application.Contracts;

public static class NtsClientDisplayFormatter
{
    public static string FormatTitle(string appName, IEnvironmentContext environment)
    {
        return FormatTitle(appName, environment.Environment);
    }

    public static string FormatTitle(string appName, string? environment)
    {
        var normalizedEnvironment = NEnvironmentNames.Normalize(environment);
        var title = $"{appName} v{ApplicationConstants.VERSION}";

        return normalizedEnvironment == NEnvironmentNames.PRODUCTION ? $"{title}" : $"{title} [{normalizedEnvironment}]";
    }
}
