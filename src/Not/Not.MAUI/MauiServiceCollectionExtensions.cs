using Not.Logging.Builder;
using Not.Logging;
using Serilog;

namespace Not.MAUI;

public static class MauiServiceCollectionExtensions
{
    /// <summary>
    /// Configures logging provider and <seealso cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}" /> service implementation
    /// </summary>
    /// <param name="mauiBuilder">MauiAppBuilder instance</param>/param>
    /// <returns>Instance of NotLogBuilder to be used to configure specific loggers</returns>
    public static NLogBuilder UseNLog(this MauiAppBuilder mauiBuilder)
    {
        mauiBuilder.Logging.AddSerilog();
        return mauiBuilder.Services.AddNLogging();
    }
}
