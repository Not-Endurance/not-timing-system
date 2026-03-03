using System.Collections;
using Microsoft.Extensions.Logging;

namespace NTS.Nexus.HTTP.Telemetry;

internal static class MaskedEnvironmentVariableLogger
{
    public static void Log(ILogger logger)
    {
        logger.LogWarning("Logging environment variables with masked values.");
        Console.WriteLine("Logging environment variables with masked values.");

        var variables = Environment
            .GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .OrderBy(x => x.Key?.ToString(), StringComparer.OrdinalIgnoreCase);

        foreach (var variable in variables)
        {
            var key = variable.Key?.ToString() ?? "<unknown>";
            var value = variable.Value?.ToString();
            var masked = Mask(value);
            logger.LogWarning("ENV {Key}={MaskedValue}", key, masked);
            Console.WriteLine($"ENV {key}={masked}");
        }
    }

    static string Mask(string? value)
    {
        if (value == null)
        {
            return "<null>";
        }
        if (value.Length == 0)
        {
            return "<empty>";
        }
        if (value.Length <= 2)
        {
            return value;
        }

        return $"{value[..2]}{new string('*', value.Length - 2)}";
    }
}
