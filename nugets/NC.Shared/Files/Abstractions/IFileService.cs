namespace Not.Files.Abstractions;

public interface IFileService
{
    Task Download(NFile file);
}
