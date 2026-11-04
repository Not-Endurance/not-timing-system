namespace Not.Files;

[Obsolete("Standardize logging and remove previous local file dependencies that require this context")]
public class FilesystemContext : IFilesystemContext
{
    readonly Func<string> _getPath;

    public FilesystemContext(Func<string> getPath)
    {
        _getPath = getPath;
    }

    public string AppDirectory => _getPath();
    public string? Name { get; set; }
}

[Obsolete("Standardize logging and remove previous local file dependencies that require this context")]
public interface IFilesystemContext
{
    string AppDirectory { get; }
    string? Name { get; set; }
}

[Obsolete("Standardize logging and remove previous local file dependencies that require this context")]
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
            Exceptions.GuardHelper.ThrowIfDefault(_applicationName);
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
