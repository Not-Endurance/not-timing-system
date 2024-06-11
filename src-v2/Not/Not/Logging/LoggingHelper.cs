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
}
