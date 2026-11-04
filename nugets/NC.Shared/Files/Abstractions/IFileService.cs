namespace NC.Files.Abstractions;

public interface IFileService
{
    Task Download(NFile file);
}
