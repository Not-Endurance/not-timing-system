using System.Text;

namespace Not.Files;

public sealed class NFile
{
    public static NFile FromText(string name, string content, string contentType)
    {
        return new NFile(name, contentType, Encoding.UTF8.GetBytes(content));
    }

    public NFile(string name, string contentType, byte[] content)
    {
        Name = name;
        ContentType = contentType;
        Content = content;
    }

    public NFile(string filePath)
    {
        Name = Path.GetFileName(filePath);
        ContentType = NFileContentTypes.FromFileName(filePath);
        Content = File.ReadAllBytes(filePath);
    }

    public string Name { get; }
    public string ContentType { get; }
    public byte[] Content { get; }

    public string ToDataUrl()
    {
        return $"data:{ContentType};base64,{Convert.ToBase64String(Content)}";
    }
}
