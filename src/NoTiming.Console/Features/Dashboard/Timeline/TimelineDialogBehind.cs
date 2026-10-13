using Microsoft.AspNetCore.Components.Web;
using Not.Blazor.Dialogs.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Dashboard.Timeline;

/// <summary>
/// The timeline of the Participation the Main Operator works on (#619, ADR-0005): every time event of every Phase, in the order
/// they were recorded, and the choosing of the ones to disable and to enable. A click stages the change of an event and applies
/// nothing, a second click takes it back, and nothing is applied until the person saves: the chosen events are written as one
/// change, against the version of the Participation that the person saw. Closing the dialog leaves the choice behind. What the
/// Api or the domain refuses is said in the dialog, with the events named, and the dialog stays open.
/// </summary>
public class TimelineDialogBehind : NDialog
{
    readonly Dictionary<Guid, bool> _staged = []; // the events chosen, each with whether the choice is to disable it

    [Inject]
    IConsoleTimeline Timeline { get; set; } = default!;

    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected IReadOnlyList<TimelinePhase> Phases { get; private set; } = [];

    protected bool IsLoaded { get; private set; }
    protected bool IsSaving { get; private set; }

    /// <summary>What was refused, in the words of the Api or the domain; nothing when the last thing asked was taken.</summary>
    protected string? Refusal { get; private set; }

    protected bool HasEvents => Phases.Any(x => x.Entries.Count > 0);
    protected bool CanSave => !IsSaving && _staged.Count > 0;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await Load();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            IsLoaded = true;
        }
    }

    protected bool IsStaged(TimelineEntry entry)
    {
        return _staged.ContainsKey(entry.Id);
    }

    /// <summary>Whether a click on the event does anything: an event that was accepted is disabled and one that was disabled is enabled, and what the rules rejected is neither.</summary>
    protected static bool IsClickable(TimelineEntry entry)
    {
        return entry.CanDisable || entry.CanEnable;
    }

    protected void Toggle(TimelineEntry entry)
    {
        if (!IsClickable(entry) || IsSaving)
        {
            return;
        }

        Refusal = null;
        if (!_staged.Remove(entry.Id))
        {
            _staged.Add(entry.Id, entry.CanDisable);
        }
    }

    /// <summary>The keys that press the row of an event, as they press a button: the Enter key and the space bar.</summary>
    protected void Press(KeyboardEventArgs key, TimelineEntry entry)
    {
        if (key.Key is "Enter" or " ")
        {
            Toggle(entry);
        }
    }

    /// <summary>What a click on the event would do, or why it would do nothing.</summary>
    protected string HintOf(TimelineEntry entry)
    {
        if (!IsClickable(entry))
        {
            return entry.OutcomeText;
        }

        return (entry.CanDisable, IsStaged(entry)) switch
        {
            (true, false) => Timeline_click_to_disable_string,
            (true, true) => Timeline_will_be_disabled_string,
            (false, false) => Timeline_click_to_enable_string,
            (false, true) => Timeline_will_be_enabled_string,
        };
    }

    /// <summary>How the row looks: what has become of the event, and what the person has chosen to make of it.</summary>
    protected string RowClassOf(TimelineEntry entry)
    {
        var state = (entry.CanDisable, entry.CanEnable) switch
        {
            (true, _) => "timeline-row--accepted",
            (_, true) => "timeline-row--disabled",
            _ => "timeline-row--rejected",
        };
        var staged = IsStaged(entry) ? " timeline-row--staged" : string.Empty;
        var shown = entry.IsShown ? " timeline-row--shown" : string.Empty;
        return $"timeline-row {state}{staged}{shown}";
    }

    protected async Task Save()
    {
        if (!CanSave)
        {
            return;
        }

        try
        {
            IsSaving = true;
            Refusal = null;
            await Dashboard.ChangeTimeEvents(
                [.. _staged.Where(x => x.Value).Select(x => x.Key)],
                [.. _staged.Where(x => !x.Value).Select(x => x.Key)]
            );
            Notifier.Success(Timeline_saved_string);
            await ConfirmDialog();
        }
        catch (DomainException ex)
        {
            Refusal = ex.Message;
            await Settle();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    async Task Load()
    {
        Phases = await Timeline.Read();
    }

    /// <summary>
    /// What is stored may have moved since the person chose, which is why a change is refused: the timeline is read again, and
    /// of what was chosen the person keeps what can still be done, so that it can be looked at again and saved over what is there now.
    /// </summary>
    async Task Settle()
    {
        try
        {
            await Load();
            var entries = Phases.SelectMany(x => x.Entries).ToDictionary(x => x.Id);
            foreach (var (id, disables) in _staged.ToList())
            {
                if (!entries.TryGetValue(id, out var entry) || (disables ? !entry.CanDisable : !entry.CanEnable))
                {
                    _staged.Remove(id);
                }
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
