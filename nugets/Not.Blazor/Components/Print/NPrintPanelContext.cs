using Not.Print;

namespace Not.Blazor.Components.Print;

public sealed record NPrintPanelContext
{
    public NPrintPanelContext(
        decimal scale,
        NPrintPaperFormat paperFormat,
        NPrintOrientation orientation
    )
    {
        Scale = scale;
        PaperFormat = paperFormat;
        Orientation = orientation;
    }

    public decimal Scale { get; }
    public NPrintPaperFormat PaperFormat { get; }
    public NPrintOrientation Orientation { get; }
}
