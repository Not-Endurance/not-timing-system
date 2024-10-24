namespace NTS.Domain.Setup.Entities;

public class Tag : DomainEntity, ISummarizable
{
    public Tag(string tagId, int number) : base(GenerateId())
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
        return Combine($"{"Tag Id".Localize()}: {TagId}");
    }
}
