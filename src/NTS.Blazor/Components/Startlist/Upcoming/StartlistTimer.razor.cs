using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Formatting;

namespace NTS.Blazor.Components.Startlist.Upcoming;

public partial class StartlistTimer
{
    TimeSpan _timerInterval = TimeSpan.FromSeconds(1);
    TimeSpan _negativeSecond = TimeSpan.FromSeconds(-1);
    TimeSpan _fiveMinutes = TimeSpan.FromMinutes(5);
    System.Timers.Timer _timer = default!;
    TimeSpan _timeInterval = default!;
    string _displayTime = default!;
    Color _color = default!;

    protected Typo Typo { get; set; } = Typo.caption;

    [Parameter]
    public DateTimeOffset StartTime { get; set; } = default!;

    [Parameter]
    public double StopTimerAtTime { get; set; } = default!;

    protected override void OnInitialized()
    {
        _timer = new(_timerInterval);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = (_timeInterval > TimeSpan.FromMinutes(StopTimerAtTime));
    }

    protected override void OnParametersSet()
    {
        var now = DateTimeOffset.Now.TimeOfDay;
        _timeInterval = StartTime.TimeOfDay - now;
        FormatTime();
        _timer.Enabled = _timeInterval > TimeSpan.FromMinutes(StopTimerAtTime);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    async void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _timeInterval = _timeInterval.Subtract(_timerInterval);
        FormatTime();
        if (_timeInterval <= TimeSpan.FromMinutes(StopTimerAtTime))
        {
            _timer.Enabled = false;
        }
        await Render();
    }

    void FormatTime()
    {
        if (_timeInterval > _negativeSecond)
        {
            _displayTime = FormattingHelper.Format(_timeInterval);
        }
        else
        {
            var positiveSignedTime = _timeInterval.Negate();
            _displayTime = " - " + FormattingHelper.Format(positiveSignedTime);
        }
        if (_timeInterval > _fiveMinutes)
        {
            _color = Color.Success;
        }
        else if (_timeInterval >= TimeSpan.Zero)
        {
            _color = Color.Warning;
        }
        else
        {
            _color = Color.Error;
        }
    }
}
