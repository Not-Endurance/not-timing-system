using Microsoft.Extensions.Localization;

namespace Not.Localization.Localizers;

public class ResxLocalizer<T> : IStringLocalizer
{
    readonly IStringLocalizer<T> _stringLocalizer;

    public ResxLocalizer(IStringLocalizer<T> stringLocalizer)
    {
        _stringLocalizer = stringLocalizer;
    }

    public LocalizedString this[string name] => _stringLocalizer[name];
    public LocalizedString this[string name, params object[] arguments] => _stringLocalizer[name, arguments];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return _stringLocalizer.GetAllStrings(includeParentCultures);
    }
}
