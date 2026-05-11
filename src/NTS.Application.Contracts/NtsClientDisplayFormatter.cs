using Not.Application.Environments;

namespace NTS.Application.Contracts;

public static class NtsClientDisplayFormatter
{
    public static string FormatTitle(string appName, NEnvironment environment)
    {
        return FormatTitle(appName, environment.Environment);
    }

    public static string FormatTitle(string appName, string? environment)
    {
        var normalizedEnvironment = NEnvionmentNames.Normalize(environment);
        var title = $"NTS {appName} v{ApplicationConstants.VERSION}";

        return normalizedEnvironment == NEnvionmentNames.PRODUCTION
            ? $"{title}"
            : $"{title} [{normalizedEnvironment}]";
    }
}
