using Not.Injection;
using Not.Notify;

namespace NTS.Domain.LocalizationDemo;

public class LocalizationTestService : ILocalizationTestService
{
    readonly INotifier _notifier;

    public LocalizationTestService(INotifier notifier)
    {
        _notifier = notifier;
    }

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
            _notifier.Warn(ex.Message);
            return ex.Message;
        }
    }
}

public interface ILocalizationTestService : ITransient
{
    string Polite();
    string Rude();
}
