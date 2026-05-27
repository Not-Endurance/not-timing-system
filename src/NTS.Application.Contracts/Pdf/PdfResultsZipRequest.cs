namespace NTS.Application.Contracts.Pdf;

public sealed class PdfResultsZipRequest
{
    public int EventId { get; init; }
    public decimal FontScale { get; init; } = 0.8m;
}
