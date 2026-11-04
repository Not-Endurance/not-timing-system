using Not.Injection;
using Not.Print;

namespace Not.Server.Print;

public interface INPrintRequestValidator : ITransient
{
    string[] Validate(NPrintDocumentRequest request);
    string[] Validate(NPrintBatchRequest request);
}
