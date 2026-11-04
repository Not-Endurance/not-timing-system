namespace NTS.Domain.Core.Objects.Documents.Base;

public abstract record Document
{
    public Document(DocumentHeader header)
    {
        Header = header;
    }

    public DocumentHeader Header { get; }
}
