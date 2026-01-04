using Not.Exceptions;
using Not.Injection.Config;

namespace Not.Filesystem;

public static class FileContextHelper
{
    /// <summary>
    /// Only used in DEBUG to provide a more stable app root directory during development<br/>
    /// PROD builds use the exe directory to store data
    /// </summary>
    /// <param name="applicatioName"></param>
    public static void SetDebugRootDirectory(string applicatioName)
    {
        _applicationName = applicatioName;
    }

    public static string ConfigureApplicationName(string applicationName)
    {
        _applicationName = applicationName;
        return _applicationName;
    }

    public static string GetAppDirectory(string subdirectory)
    {
        var basePath =
#if DEBUG
            $"C:\\tmp\\{_applicationName}";
        GuardHelper.ThrowIfDefault(_applicationName);
#else
        Directory.GetCurrentDirectory();
#endif
        return Path.Combine(basePath, subdirectory);
    }

    public static Func<IServiceProvider, object?, FileContext> CreateFileContextFactory(
        Action<FileContext>? configure,
        string defaultDirectoryName
    )
    {
        return configure == null ? CreateFileContextFactory(defaultDirectoryName) : CreateFileContextFactory(configure);
    }

    static string? _applicationName;

    static Func<IServiceProvider, object?, FileContext> CreateFileContextFactory(string directory)
    {
        var context = new FileContext { Path = GetAppDirectory(directory) };
        (context as INConfig).Validate();
        return (_, __) => context;
    }

    static Func<IServiceProvider, object?, FileContext> CreateFileContextFactory(Action<FileContext> configure)
    {
        var context = new FileContext();
        configure(context);
        (context as INConfig).Validate();
        return (_, __) => context;
    }
}
