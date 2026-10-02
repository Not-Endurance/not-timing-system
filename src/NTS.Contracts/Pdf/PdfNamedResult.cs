namespace NTS.Contracts.Pdf;

public sealed class PdfNamedResult
{
    public PdfNamedResult(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }
    public string Name { get; }
}
