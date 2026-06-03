namespace Not.Print;

public sealed class NPrintDocumentRequest
{
    public string TemplateId { get; init; } = NPrintTemplateIds.Default;
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Html { get; init; } = string.Empty;
    public NPrintPageOptions Page { get; init; } = new();
}
