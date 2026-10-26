using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NoTiming.Ui.Constants;
using NTS.Contracts.Features.Console;
using NTS.Domain.Enums;

namespace NoTiming.Console.Features.Dashboard;

/// <summary>
/// A time the Console Operator names for the Participation it works on, recorded as an Arrival or as a Presentation (#646):
/// the server records it as any other Snapshot, accepted or rejected with its reason, and says which.
/// </summary>
public class TimePanelBehind : NComponent
{
    const string DEFAULT_TIME = "00:00:00";

    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected static PatternMask TimeMask { get; } = new(Masks.SECONDS_TIME_MASK_FORMAT);

    protected string? Input { get; set; } = DEFAULT_TIME;

    protected void Now()
    {
        Input = DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss");
    }

    protected Task Arrive()
    {
        return Record(SnapshotType.Arrive);
    }

    protected Task Present()
    {
        return Record(SnapshotType.Present);
    }

    async Task Record(SnapshotType type)
    {
        try
        {
            if (Input is null or DEFAULT_TIME)
            {
                Notifier.Inform(Time_format_is_incorrect_hrs_colon_mins_colon_secs_string); // there is no time to record
                return;
            }

            if (!TimeSpan.TryParse(Normalize(Input), out var timeOfDay))
            {
                Notifier.Inform(Time_format_is_incorrect_hrs_colon_mins_colon_secs_string);
                return;
            }

            await Dashboard.RecordTime(type, timeOfDay);
            Notifier.Success(Time_recorded_string);
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>A time typed with its parts missing or short: <c>7:5</c> is 07:05:00.</summary>
    static string Normalize(string input)
    {
        var parts = input
            .Split(':')
            .Select(x =>
                x.Length switch
                {
                    0 => "00",
                    1 => '0' + x,
                    _ => x,
                }
            )
            .ToList();
        while (parts.Count < 3)
        {
            parts.Add("00");
        }

        return string.Join(':', parts);
    }
}
