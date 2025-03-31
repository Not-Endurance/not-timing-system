using Not.Domain.Base;

namespace NTS.Domain.Setup.Aggregates;

public class Tag : AggregateRoot, IAggregateRoot
{
    public Tag(string tagId, int number)
        : base(GenerateId())
    {
        TagId = tagId;
        Number = number;
    }

    public string TagId { get; }
    public int Number { get; }

    public string PrepareToWrite()
    {
        const char EMPTY_CHAR = '0';
        var number = Number.ToString().PadLeft(3, EMPTY_CHAR);
        var position = "".PadLeft(6, EMPTY_CHAR); // present for legacy compatibility
        return number + position + TagId;
    }

    public override string ToString()
    {
        return $"{"Tag Id"}: {TagId}";
    }
}
