using Microsoft.Extensions.Localization;
using Not.Localization;

namespace NTS.Tests.Unit.Temporary;

public sealed class LocalizationHelperTests
{
    [Fact]
    public void LocalizeEnum_uses_enum_name_without_display_attribute()
    {
        var localizer = new TestLocalizer(new Dictionary<string, string>());
        LocalizationHelper.Configure(localizer);

        try
        {
            Assert.Equal("Landscape", NStrings.Localize(Not.Print.NPrintOrientation.Landscape));
        }
        finally
        {
            LocalizationHelper.Clear(localizer);
        }
    }

    [Fact]
    public void LocalizeEnum_falls_back_to_enum_name_without_display_name()
    {
        var localizer = new TestLocalizer(new Dictionary<string, string>());
        LocalizationHelper.Configure(localizer);

        try
        {
            Assert.Equal(nameof(TestEnum.Value), NStrings.Localize(TestEnum.Value));
        }
        finally
        {
            LocalizationHelper.Clear(localizer);
        }
    }

    sealed class TestLocalizer : IStringLocalizer
    {
        readonly IReadOnlyDictionary<string, string> _values;

        public TestLocalizer(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public LocalizedString this[string name] =>
            _values.TryGetValue(name, out var value)
                ? new LocalizedString(name, value)
                : new LocalizedString(name, name, true);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(this[name].Value, arguments), this[name].ResourceNotFound);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
        }
    }

    enum TestEnum
    {
        Value,
    }
}
