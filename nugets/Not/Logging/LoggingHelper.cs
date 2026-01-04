using Serilog;

namespace Not.Logging;

public static class LoggingHelper
{
    public static void Debug(string message)
    {
        Log.Debug(message);
    }

    public static void Information(string message)
    {
        Log.Information(message);
    }

    public static void Error(string message)
    {
        Log.Error(message);
    }

    static bool _isLoggerConfigured;

    internal static void Validate()
    {
        if (_isLoggerConfigured)
        {
            throw new Exception(
                "Serilog static logger is already configured. Second logger is unsupported as it will simply replace the original"
            );
        }
        _isLoggerConfigured = true;
    }
}
