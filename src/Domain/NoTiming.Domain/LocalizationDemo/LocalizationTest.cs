using NC.Domain;
using NC.Domain.Exceptions;

namespace NoTiming.Domain.LocalizationDemo;

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
