using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Application.Core;
using NTS.Blazor.Constants;
using NTS.Domain.Aggregates;
using NTS.Domain.Objects;
using NTS.Judge.Contracts.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Snapshots;

public class SnapshotPanelBehind : NComponent
{
    const string DEFAULT_TIME = "00:00:00";

    [Inject]
    IParticipationContext ParticipationContext { get; set; } = default!;

    [Inject]
    ISnapshotService TimingService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected static PatternMask TimeMask { get; } = new(Masks.SECONDS_TIME_MASK_FORMAT);

    protected string? Input { get; set; } = DEFAULT_TIME;

    protected Task Snapshot()
    {
        try
        {
            var currentTime = DateTime.Now.TimeOfDay;
            Input = currentTime.ToString();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }

        return Task.CompletedTask;
    }

    protected async Task SnapshotTime()
    {
        try
        {
            if (Input is null or DEFAULT_TIME)
            {
                return;
            }

            var timeString = NormalizeInput(Input);
            if (!TimeSpan.TryParse(timeString, out var timeSpan))
            {
                Notifier.Inform(Time_format_is_incorrect_hrs_colon_mins_colon_secs_string);
                return;
            }

            var time = DateTime.Today + timeSpan;
            var timestamp = new Timestamp(time);
            var snapshot = new Snapshot(
                ParticipationContext.Selected!.Combination.Number,
                SnapshotType.Automatic,
                SnapshotMethod.Manual,
                timestamp
            );
            await TimingService.Record(snapshot);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected string NormalizeInput(string input)
    {
        try
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
        catch (Exception ex)
        {
            Handle(ex);
            return input;
        }
    }
}
