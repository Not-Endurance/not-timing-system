using System.Text.RegularExpressions;
using NTS.Blazor.Constants;
using NTS.Domain.Objects;

namespace NTS.Witness.Components.Pages.Snapshot;

public class TimestampUpdateModel
{
    public TimestampUpdateModel(TimeSpan initial)
    {
        TimestampTime = initial;
    }

    //string? _timestampInput;

    //public TimestampUpdateModel(Timestamp timestamp)
    //{
    //    TimestampInput = timestamp.ToString();
    //}

    //public string? TimestampInput
    //{
    //    get
    //    {
    //        return _timestampInput;
    //    }
    //    set
    //    {
    //        if (!string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, @"^\d{2}:\d{2}:\d{2}$"))
    //        {
    //            _timestampInput = value;
    //        }
    //    }

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
