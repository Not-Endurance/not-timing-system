using Not.Injection;
using Not.Notify;

namespace NTS.Domain.LocalizationDemo;

public class LocalizationTestService : ILocalizationTestService
{
    public string Polite()
    {
        return new LocalizationTest().Success();
    }

    public string Rude()
    {
        try
        {
            return new LocalizationTest().Invalid();
        }
        catch (Exception ex)
        {
            NotifyHelper.Warn(ex.Message);
            return ex.Message;
        }
    }
}

public interface ILocalizationTestService : ITransient
{
    string Polite();
    string Rude();
}
