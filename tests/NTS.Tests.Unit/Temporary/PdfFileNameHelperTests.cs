using NTS.Application.Contracts.Pdf;

namespace NTS.Tests.Unit.Temporary;

public class PdfFileNameHelperTests
{
    [Fact]
    public void ResultPdfEntries_UsesSanitizedResultNames()
    {
        var entries = PdfFileNameHelper.ResultPdfEntries(
            [new PdfNamedResult(10, "CEI 1* / Senior"), new PdfNamedResult(11, "CEI 2*")]
        );

        Assert.Collection(
            entries,
            first => Assert.Equal("CEI-1-Senior.pdf", first.EntryName),
            second => Assert.Equal("CEI-2.pdf", second.EntryName)
        );
    }

    [Fact]
    public void ResultPdfEntries_DisambiguatesDuplicateNames()
    {
        var entries = PdfFileNameHelper.ResultPdfEntries(
            [new PdfNamedResult(10, "CEI 1*"), new PdfNamedResult(11, "CEI 1*")]
        );

        Assert.Collection(
            entries,
            first => Assert.Equal("CEI-1.pdf", first.EntryName),
            second => Assert.Equal("CEI-1-11.pdf", second.EntryName)
        );
    }
}
