namespace Not.Filesystem;

public class FileHelper
{
    public static async Task Write(string path, string content)
    {
        if (!File.Exists(path))
        {
            var directoryPath = path[..path.LastIndexOf('\\')];
            Directory.CreateDirectory(directoryPath);
        }
        await File.WriteAllTextAsync(path, content);
    }

    public static async Task<string?> SafeReadString(string path)
    {
        try
        {
            return await File.ReadAllTextAsync(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
