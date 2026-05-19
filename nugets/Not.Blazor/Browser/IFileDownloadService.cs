namespace Not.Blazor.Browser;

public interface IFileDownloadService
{
    Task DownloadText(string fileName, string content, string contentType);
}
