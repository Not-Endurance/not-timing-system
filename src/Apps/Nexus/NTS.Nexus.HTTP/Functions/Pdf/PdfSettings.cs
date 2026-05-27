namespace NTS.Nexus.HTTP.Functions.Pdf;

public sealed class PdfSettings
{
    public string? PrintBaseUrl { get; init; }
    public int RenderTimeoutSeconds { get; init; } = 60;
}
