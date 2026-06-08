using System.Net;
using System.Reflection;
using Not.Print;

namespace Not.Server.Print;

public sealed class NPrintTemplateRenderer : INPrintTemplateRenderer
{
    const string TEMPLATE_RESOURCE = "Not.Server.Print.Templates.default-print.html";
    const string MUD_BLAZOR_STYLES_RESOURCE = "Not.Server.Print.Assets.MudBlazor.min.css";
    static readonly Lazy<string> TEMPLATE = new(() => LoadResource(TEMPLATE_RESOURCE));
    static readonly Lazy<string> MUD_BLAZOR_STYLES = new(() => LoadResource(MUD_BLAZOR_STYLES_RESOURCE));

    public string Render(NPrintDocumentRequest request)
    {
        return TEMPLATE
            .Value.Replace("{{Title}}", H(request.Title), StringComparison.Ordinal)
            .Replace("{{MudBlazorStyles}}", MUD_BLAZOR_STYLES.Value, StringComparison.Ordinal)
            .Replace("{{Styles}}", NPrintDocumentCss.Create(request.Page), StringComparison.Ordinal)
            .Replace("{{Backdrop}}", CreateBackdrop(request), StringComparison.Ordinal)
            .Replace("{{Footer}}", CreateFooter(request), StringComparison.Ordinal)
            .Replace("{{Body}}", request.Html, StringComparison.Ordinal);
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

    static string LoadResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream =
            assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded print resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
