namespace NTS.Contracts.Pdf;

public sealed class PdfNamedResult
{
    public PdfNamedResult(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }
    public string Name { get; }
}
