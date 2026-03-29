using Not.DateAndTime;

namespace NTS.Judge.Tests.Domain;

public class DateTimeExtensionsTests
{
    [Fact]
    public void ToLocalDateTime_WhenDateCrossesDstBoundary_UsesOffsetForCombinedLocalTime()
    {
        var sofia = ResolveTimeZone("Europe/Sofia", "FLE Standard Time");
        var date = new DateTime(2026, 3, 29);
        var localTime = new DateTime(2026, 3, 29, 8, 0, 0, DateTimeKind.Unspecified);

        var result = date.ToLocalDateTime(TimeSpan.FromHours(8), sofia);

        Assert.Equal(new DateTimeOffset(localTime, sofia.GetUtcOffset(localTime)), result);
        Assert.Equal(TimeSpan.FromHours(3), result.Offset);
        Assert.NotEqual(sofia.GetUtcOffset(date), result.Offset);
    }

    static TimeZoneInfo ResolveTimeZone(params string[] ids)
    {
        foreach (var id in ids)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("Could not resolve a Sofia time zone on this machine.");
    }
}
