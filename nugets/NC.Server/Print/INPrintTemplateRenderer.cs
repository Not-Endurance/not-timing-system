using NC.Injection;
using NC.Print;

namespace NC.Server.Print;

public interface INPrintTemplateRenderer : ITransient
{
    string Render(NPrintDocumentRequest request);
}
