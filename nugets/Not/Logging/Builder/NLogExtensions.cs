using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Injection.Config;
using Not.Logging.Filesystem;
using Not.Logging.HTTP;
using Not.Startup;

namespace Not.Logging.Builder;

public static class NLogExtensions
{
    public static NLogBuilder AddHttpLogger(this NLogBuilder builder, Action<HttpLoggerContext> configure)
    {
        var factory = NConfigHelper.CreateConfigFactory(configure);
        builder.Services.AddSingleton(factory).AddSingleton<IStartupInitializer, HttpLoggerInitializer>();

        return builder;
    }

    /// <summary>
    /// FileContext defaults to <seealso cref="FileContextHelper.GetAppDirectory(string)"/> using 'logs' as subdirectory
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure">Custom configuration</param>
    /// <returns></returns>
    public static NLogBuilder AddFilesystemLogger(this NLogBuilder builder, string appName)
    {
        var factory = FileContextHelper.CreateFileContextFactory("logs", appName);
        builder
            .Services.AddKeyedSingleton<IFilesystemContext, FilesystemContext>(NLogBuilder.KEY, factory)
            .AddSingleton<IStartupInitializer, FilesystemLoggerInitalizer>();

        return builder;
    }
}
