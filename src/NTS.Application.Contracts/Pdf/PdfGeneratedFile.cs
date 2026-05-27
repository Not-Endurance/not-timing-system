namespace NTS.Application.Contracts.Pdf;

public sealed class PdfGeneratedFile
{
    public PdfGeneratedFile(string fileName, string contentType, byte[] content)
    {
        FileName = fileName;
        ContentType = contentType;
        Content = content;
    }

    public string FileName { get; }
    public string ContentType { get; }
    public byte[] Content { get; }
}
