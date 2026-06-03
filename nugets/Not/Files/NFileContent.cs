using System.Text;

namespace Not.Files;

public sealed class NFileContent
{
    public static NFileContent FromText(string fileName, string content, string contentType)
    {
        return new NFileContent(fileName, contentType, Encoding.UTF8.GetBytes(content));
    }

    public NFileContent(string fileName, string contentType, byte[] content)
    {
        FileName = fileName;
        ContentType = contentType;
        Content = content;
    }

    public string FileName { get; }
    public string ContentType { get; }
    public byte[] Content { get; }

    public string ToDataUrl()
    {
        return $"data:{ContentType};base64,{Convert.ToBase64String(Content)}";
    }
}
