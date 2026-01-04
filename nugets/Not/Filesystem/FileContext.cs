using Not.Injection.Config;

namespace Not.Filesystem;

public class FileContext : INConfig, IFileContext // TODO: move to Filesystem
{
    public string Path { get; set; } = default!;
    public string? Name { get; set; }

    void INConfig.Validate()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            throw new ArgumentException($"'{nameof(IFileContext)}.{nameof(Path)}' cannot be null or whitespace");
        }
    }
}

public interface IFileContext : INConfig
{
    string Path { get; set; }
    string? Name { get; set; }
}
