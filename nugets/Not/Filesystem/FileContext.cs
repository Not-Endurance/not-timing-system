using Not.Injection.Config;

namespace Not.Filesystem;

public class FileContext : IFileContext // TODO: move to Filesystem
{
    readonly Func<string> _getPath;

    public FileContext(Func<string> getPath)
    {
        _getPath = getPath;
    }

    public string Path => _getPath();
    public string? Name { get; set; }
}

public interface IFileContext
{
    string Path { get; }
    string? Name { get; set; }
}
