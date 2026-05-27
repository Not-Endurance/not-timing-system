using Microsoft.JSInterop;
using NTS.Application.Contracts.Pdf;

namespace NTS.Judge.Blazor.Features.Print;

public class JudgePdfBrowserService : IJudgePdfBrowserService
{
    readonly IJSRuntime _jsRuntime;

    public JudgePdfBrowserService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task PrintPdf(PdfGeneratedFile file)
    {
        await using var module = await Import();
        await module.InvokeVoidAsync("printPdfBytes", file.Content);
    }

    public async Task Download(PdfGeneratedFile file)
    {
        await using var module = await Import();
        await module.InvokeVoidAsync("downloadBytes", file.FileName, file.Content, file.ContentType);
    }

    ValueTask<IJSObjectReference> Import()
    {
        return _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/NTS.Judge.Blazor/judge-pdf.js");
    }
}
