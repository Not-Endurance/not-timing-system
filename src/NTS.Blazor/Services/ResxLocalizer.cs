using Microsoft.Extensions.Localization;
using Not.Injection;
using NTS.Localization.Resources;

namespace NTS.Blazor.Services;

public class ResxLocalizer : IStringLocalizer, ISingleton
{
    readonly IStringLocalizer<LocalizedStrings> _stringLocalizer;

    public ResxLocalizer(IStringLocalizer<LocalizedStrings> stringLocalizer)
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
