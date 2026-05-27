namespace NTS.Application.Contracts.Pdf;

public sealed class PdfDocumentRequest
{
    public PdfDocumentType Type { get; init; }
    public int EventId { get; init; }
    public int? RankingId { get; init; }
    public decimal FontScale { get; init; } = 1m;
    public PdfPaperFormat PaperFormat { get; init; } = PdfPaperFormat.A4;
    public PdfOrientation Orientation { get; init; } = PdfOrientation.Portrait;
}
