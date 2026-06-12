using Not.Injection;
using Not.Print;

namespace Not.Server.Print;

public interface INPrintTemplateRenderer : ITransient
{
    string Render(NPrintDocumentRequest request);
}
