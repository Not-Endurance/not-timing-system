#pragma warning disable IDE0052
namespace Not.Filesystem;

public static class FileContextHelper
{
    // TODO: refactor this mess
    public static Func<IServiceProvider, object?, FilesystemContext> CreateFileContextFactory(
        string directoryName,
        string? appName = null
    )
    {
        if (appName != null && _applicationName == null)
        {
            _applicationName = appName;
        }
        var context = new FilesystemContext(() =>
        {
            var basePath =
#if DEBUG
                $"C:\\tmp\\{_applicationName}.debug";
            Not.Exceptions.GuardHelper.ThrowIfDefault(_applicationName);
#else
            Directory.GetCurrentDirectory();
#endif
            return Path.Combine(basePath, directoryName);
        });

        return (_, __) => context;
    }

    static string? _applicationName;
}
#pragma warning restore IDE0052
