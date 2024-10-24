namespace NTS.Domain;

public class LocalizationTest : DomainEntity
{
    public LocalizationTest() : base(GenerateId())
    {
    }

    public string Success()
        => "My dick Yanko";

    public string Invalid()
        => throw new DomainException("Kur {0}", "debel");
}
