namespace NTS.Domain.Core.Objects;

public record Document : DomainObject
{
    public Document(DocumentHeader header)
    {
        Header = header;
    }

    public DocumentHeader Header { get; }
}
