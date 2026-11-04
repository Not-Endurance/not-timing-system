using System.Net;
using Not.Print;

namespace Not.Blazor.Client.Browser;

internal static class NBrowserPrintHtmlRenderer
{
    const string MUD_BLAZOR_STYLESHEET = "./_content/MudBlazor/MudBlazor.min.css";

    public static string Render(NPrintDocumentRequest request)
    {
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>{{H(request.Title)}}</title>
                <link rel="stylesheet" href="{{MUD_BLAZOR_STYLESHEET}}">
                <style data-source="not-print">
            {{NPrintDocumentCss.Create(request.Page)}}
                </style>
            </head>
            <body>
            {{CreateBackdrop(request)}}
            {{CreateFooter(request)}}
            {{request.Html}}
            </body>
            </html>
            """;
    }

    static string CreateBackdrop(NPrintDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BackdropImage))
        {
            return string.Empty;
        }

        return $"""<div class="print-backdrop" aria-hidden="true"><img src="{H(request.BackdropImage)}" alt=""></div>""";
    }

    static string CreateFooter(NPrintDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FooterText))
        {
            return string.Empty;
        }

        return $"""<div class="print-footer">{H(request.FooterText)}</div>""";
    }

    static string H(object? value)
    {
        return WebUtility.HtmlEncode(value?.ToString() ?? string.Empty);
    }
}
