using MudBlazor;
using Not.Notify;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Snapshots;

public partial class SnapshotPanel
{
    const string DEFAULT_TIME = "00:00:00";
    static readonly PatternMask TIME_MASK = new("00:00:00");

    string? _input = DEFAULT_TIME;

    [Inject]
    IManualProcessor ManualProcessor { get; set; } = default!;

    Task Snapshot()
    {
        var currentTime = DateTime.Now.TimeOfDay;
        _input = currentTime.ToString();
        return Task.CompletedTask;
    }

    async Task Process()
    {
        if (_input is null or DEFAULT_TIME)
        {
            return;
        }
        var timeString = NormalizeInput(_input);
        if (!TimeSpan.TryParse(timeString, out var timeSpan))
        {
            NotifyHelper.Inform(Time_format_is_incorrect_hrs_colon_mins_colon_secs_string);
            return;
        }
        var time = DateTime.Today + timeSpan;
        var timestamp = new Timestamp(time);
        await ManualProcessor.Process(timestamp);
    }

    string NormalizeInput(string input)
    {
        var values = input
            .Split(':')
            .Select(x =>
            {
                if (x == string.Empty)
                {
                    return "00";
                }
                if (x.Length == 1)
                {
                    return '0' + x;
                }
                return x;
            });
        return string.Join(':', values);
    }
}
