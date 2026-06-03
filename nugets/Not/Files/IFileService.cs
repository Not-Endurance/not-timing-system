namespace Not.Files;

public interface IFileService
{
    Task Download(NFileContent file);
}
