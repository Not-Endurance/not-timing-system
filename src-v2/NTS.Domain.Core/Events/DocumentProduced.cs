using Not.Events;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Events;

public record DocumentProduced : DomainObject
{
    public DocumentProduced(Document document)
    {
        Document = document;
    }

    public Document Document { get; }
}
