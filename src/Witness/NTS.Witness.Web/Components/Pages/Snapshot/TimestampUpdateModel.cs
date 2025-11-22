using System.Text.RegularExpressions;
using NTS.Blazor.Constants;
using NTS.Domain.Objects;

namespace NTS.Witness.Web.Components.Pages.Snapshot;

public class TimestampUpdateModel
{
    public TimestampUpdateModel(TimeSpan initial)
    {
        TimestampTime = initial;
    }

    public TimeSpan TimestampTime { get; set; }

    public string TimestampInput
    {
        get => TimestampTime.ToString(@"hh\:mm\:ss");
        set
        {
            if (TimeSpan.TryParseExact(value, @"hh\:mm\:ss", null, out var ts))
            {
                TimestampTime = ts;
            }
        }
    }
}
