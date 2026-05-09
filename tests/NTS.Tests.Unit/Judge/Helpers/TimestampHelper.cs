using NTS.Domain.Objects;

namespace NTS.Judge.Tests.Helpers;

internal class TimestampHelper
{
    public static Timestamp Create(int hour, int? minute = null, int? second = null)
    {
        return new Timestamp(new DateTime(1, 1, 1, hour, minute ?? 0, second ?? 0));
    }
}
