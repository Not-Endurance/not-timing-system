using System.Globalization;
using NC.Localization;
using Xunit;

namespace NC.Tests.Localization;

public sealed class CultureScopeTests
{
    [Fact]
    public void Dispose_WhenScopeEnds_ShouldRestorePreviousCulture()
    {
        var previous = CultureInfo.CurrentUICulture.Name;

        using (new CultureScope("tr"))
        {
            Assert.Equal("tr", CultureInfo.CurrentUICulture.Name);
        }

        Assert.Equal(previous, CultureInfo.CurrentUICulture.Name);
    }
}
