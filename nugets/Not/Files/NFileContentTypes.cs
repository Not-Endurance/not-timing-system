namespace Not.Files;

public static class NFileContentTypes
{
    public const string Binary = "application/octet-stream";
    public const string Csv = "text/csv";
    public const string Gif = "image/gif";
    public const string Html = "text/html";
    public const string Jpeg = "image/jpeg";
    public const string Pdf = "application/pdf";
    public const string Png = "image/png";
    public const string Svg = "image/svg+xml";
    public const string Text = "text/plain";
    public const string Webp = "image/webp";
    public const string Xml = "application/xml";
    public const string Zip = "application/zip";

    public static string FromFileName(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => Csv,
            ".gif" => Gif,
            ".htm" or ".html" => Html,
            ".jpg" or ".jpeg" => Jpeg,
            ".pdf" => Pdf,
            ".png" => Png,
            ".svg" => Svg,
            ".txt" => Text,
            ".webp" => Webp,
            ".xml" => Xml,
            ".zip" => Zip,
            _ => Binary,
        };
    }
}
