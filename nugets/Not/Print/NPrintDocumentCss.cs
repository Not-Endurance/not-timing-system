using System.Globalization;

namespace Not.Print;

public static class NPrintDocumentCss
{
    public static string Create(NPrintPageOptions options)
    {
        var paperSize = $"{FormatPaperSize(options.PaperFormat)} {options.Orientation.ToString().ToLowerInvariant()}";
        return $$"""
            @page {
                size: {{paperSize}};
                margin: {{options.Margin}};
            }

            html,
            body {
                --print-font-scale: {{options.Scale.ToString(CultureInfo.InvariantCulture)}};
                width: 100%;
                min-height: 100%;
                margin: 0;
                background: #fff !important;
            }

            html {
                font-size: calc(16px * var(--print-font-scale));
            }

            body {
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
                color: #111;
                font-family: Roboto, Arial, Helvetica, sans-serif;
            }

            .print-document {
                box-sizing: border-box;
                width: 100%;
                color: #111;
                background: #fff;
                font-size: calc(14px * var(--print-font-scale));
                line-height: 1.25;
            }

            .print-document *,
            .print-document *::before,
            .print-document *::after {
                box-sizing: border-box;
            }

            .no-print,
            nav,
            header,
            footer,
            .mud-appbar,
            .mud-drawer,
            .n-content-rightbar {
                display: none !important;
            }

            .print-page-break {
                break-before: page;
                page-break-before: always;
            }

            .print-avoid-break {
                break-inside: avoid;
                page-break-inside: avoid;
            }

            .handout-print-page {
                break-after: page;
                page-break-after: always;
                min-height: calc(100vh - 1px);
                overflow: hidden;
            }

            .handout-print-page:last-child {
                break-after: auto;
                page-break-after: auto;
            }

            table {
                width: 100%;
                border-collapse: collapse;
                break-inside: auto;
                page-break-inside: auto;
            }

            thead {
                display: table-header-group;
            }

            tfoot {
                display: table-footer-group;
            }

            tr,
            td,
            th {
                break-inside: avoid;
                page-break-inside: avoid;
            }

            h1,
            h2,
            h3,
            h4,
            h5,
            h6,
            p {
                margin: 0;
            }

            img {
                max-width: 100%;
            }

            .d-flex,
            .mud-stack {
                display: flex !important;
            }

            .flex-row {
                flex-direction: row !important;
            }

            .flex-column,
            .mud-stack {
                flex-direction: column;
            }

            .flex-wrap {
                flex-wrap: wrap !important;
            }

            .justify-center {
                justify-content: center !important;
            }

            .justify-space-around {
                justify-content: space-around !important;
            }

            .justify-flex-start {
                justify-content: flex-start !important;
            }

            .align-center {
                align-items: center !important;
            }

            .align-end {
                align-items: flex-end !important;
            }

            .mt-1 {
                margin-top: 4px !important;
            }

            .mb-3 {
                margin-bottom: 12px !important;
            }

            .mb-4 {
                margin-bottom: 16px !important;
            }

            .mr-4 {
                margin-right: 16px !important;
            }

            .pb-5 {
                padding-bottom: 20px !important;
            }

            .pt-2 {
                padding-top: 8px !important;
            }

            .mud-spacer {
                flex-grow: 1;
            }

            .mud-paper {
                color: inherit;
                background-color: transparent;
            }

            .mud-divider {
                border: 0;
                border-top: 1px solid #d7d7d7;
                width: 100%;
                margin: 8px 0;
            }

            .mud-typography {
                font-family: inherit;
                line-height: inherit;
            }

            .mud-typography-h5 {
                font-size: 1.5em;
                line-height: 1.25;
                font-weight: 400;
            }

            .mud-typography-h6 {
                font-size: 1.25em;
                line-height: 1.25;
                font-weight: 400;
            }

            .mud-typography-button {
                font-size: 0.875em;
                line-height: 1.5;
                font-weight: 500;
            }

            .mud-typography-subtitle2 {
                font-size: 0.875em;
                line-height: 1.45;
                font-weight: 500;
            }

            .mud-typography-body2 {
                font-size: 0.875em;
                line-height: 1.43;
                font-weight: 400;
            }

            .mud-typography-caption {
                font-size: 0.75em;
                line-height: 1.4;
                font-weight: 400;
            }

            .mud-typography-align-center {
                text-align: center;
            }

            .mud-image-object-fit-scale-down {
                object-fit: scale-down;
            }

            .participation-table .mud-button-label {
                font-weight: normal !important;
            }
            """;
    }

    static string FormatPaperSize(NPrintPaperFormat paperFormat)
    {
        return paperFormat switch
        {
            NPrintPaperFormat.Letter => "Letter",
            NPrintPaperFormat.Legal => "Legal",
            NPrintPaperFormat.Tabloid => "Tabloid",
            NPrintPaperFormat.Ledger => "Ledger",
            _ => paperFormat.ToString().ToUpperInvariant(),
        };
    }
}
