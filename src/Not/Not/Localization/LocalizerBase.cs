using Not.Injection;

namespace Not.Localization;

public abstract class LocalizerBase : ILocalizer
{
    protected abstract string GetLocalizedValue(string key);

    public string Get(params object[] args)
    {
        var localized = args.Where(x => x != null).Select(x => GetLocalizedValue(x.ToString()!)).ToArray();
        if (localized.Length == 0)
        {
            return "";
        }
        return string.Join(" ", localized);
    }
}

public interface ILocalizer
{
    string Get(params object[] args);
}
