using Not.Logging;
using Serilog;

namespace Not.MAUI.Logging;

public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Configures logging provider and <seealso cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}" /> service implementation
    /// </summary>
    /// <param name="mauiBuilder">MauiAppBuilder instance</param>/param>
    /// <returns>Instance of NotLogBuilder to be used to configure specific loggers</returns>
    public static NotLogBuilder ConfigureLogging(this MauiAppBuilder mauiBuilder)
    {
        mauiBuilder.Logging.AddSerilog();
        mauiBuilder.Services.AddSerilog();
        return new NotLogBuilder(mauiBuilder.Services);
    }
}
