using Not.Domain;
using Not.Domain.Exceptions;

namespace NTS.Domain.LocalizationDemo;

public class LocalizationTest : Aggregate
{
    public LocalizationTest()
        : base(null) { }

    public string Success()
    {
        return "My dick Yanko";
    }

    public string Invalid()
    {
        throw new DomainException("Kur {0}", "debel");
    }
}
