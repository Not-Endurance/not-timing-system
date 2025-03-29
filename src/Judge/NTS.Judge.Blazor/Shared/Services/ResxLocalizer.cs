using Microsoft.Extensions.Localization;
using Not.Injection;
using NTS.Judge.Blazor.Resources.Localization;

namespace NTS.Judge.Blazor.Shared.Services;

public class ResxLocalizer : IStringLocalizer, ISingleton
{
    readonly IStringLocalizer<Strings> _stringLocalizer;

    public ResxLocalizer(IStringLocalizer<Strings> stringLocalizer)
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
