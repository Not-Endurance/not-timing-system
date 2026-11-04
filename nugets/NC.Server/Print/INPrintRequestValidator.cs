using NC.Injection;
using NC.Print;

namespace NC.Server.Print;

public interface INPrintRequestValidator : ITransient
{
    string[] Validate(NPrintDocumentRequest request);
    string[] Validate(NPrintBatchRequest request);
}
