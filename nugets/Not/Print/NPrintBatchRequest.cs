namespace Not.Print;

public sealed class NPrintBatchRequest
{
    public string FileName { get; init; } = string.Empty;
    public IReadOnlyList<NPrintDocumentRequest> Documents { get; init; } = [];
}
