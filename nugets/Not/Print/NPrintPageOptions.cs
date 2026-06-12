namespace Not.Print;

public sealed class NPrintPageOptions
{
    public NPrintPaperFormat PaperFormat { get; init; } = NPrintPaperFormat.A4;
    public NPrintOrientation Orientation { get; init; } = NPrintOrientation.Portrait;
    public decimal Scale { get; init; } = 1m;
    public string Margin { get; init; } = "10mm";
}
