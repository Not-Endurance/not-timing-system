using Not.Injection.Config;

namespace Not.Filesystem;

public class FilesystemContext : IFilesystemContext // TODO: move to Filesystem
{
    readonly Func<string> _getPath;

    public FilesystemContext(Func<string> getPath)
    {
        _getPath = getPath;
    }

    public string AppDirectory => _getPath();
    public string? Name { get; set; }
}

public interface IFilesystemContext
{
    string AppDirectory { get; }
    string? Name { get; set; }
}
