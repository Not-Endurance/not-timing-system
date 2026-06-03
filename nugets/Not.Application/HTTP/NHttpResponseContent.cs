namespace Not.Application.HTTP;

public sealed class NHttpResponseContent
{
    public NHttpResponseContent(byte[] content, string? contentType, string? fileName)
    {
        Content = content;
        ContentType = contentType;
        FileName = fileName;
    }

    public byte[] Content { get; }
    public string? ContentType { get; }
    public string? FileName { get; }
}
