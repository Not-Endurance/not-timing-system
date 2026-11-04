using NC.Application.Environments;

namespace NoTiming.Application.Contracts;

public static class NoTimingClientDisplayFormatter
{
    public static string FormatTitle(string appName, NEnvironment environment)
    {
        return FormatTitle(appName, environment.Environment);
    }

    public static string FormatTitle(string appName, string? environment)
    {
        var normalizedEnvironment = NEnvionmentNames.Normalize(environment);
        var title = $"{appName} v{ApplicationConstants.VERSION}";

        return normalizedEnvironment == NEnvionmentNames.PRODUCTION ? $"{title}" : $"{title} [{normalizedEnvironment}]";
    }
}
