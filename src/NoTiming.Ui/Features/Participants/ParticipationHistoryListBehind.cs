using System.Globalization;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// The Participations of an Athlete or a Horse as rows, newest first (#639, ADR-0010): what it takes to recognise one at a
/// glance in the header of its row, and its Phases and totals as the Results show them, with the other Rankings that count it,
/// when the row is opened. It reads a page at a time, and a Participation only when its row is opened. It is used inline, in a
/// dialog and on the search page.
/// </summary>
public class ParticipationHistoryListBehind : NComponent
{
    protected const int COLUMNS = 6;
    public const int PAGE_SIZE = 20;

    readonly HashSet<Guid> _open = [];
    readonly Dictionary<Guid, Participation?> _read = [];
    List<HistoricParticipation> _rows = [];
    (ParticipantKind Kind, Guid Id)? _shown;
    int _turn; // changes with the Participant that is listed, so that what was asked for one is not taken for what is asked for the next
    int _pages;

    [Inject]
    IParticipationHistory History { get; set; } = default!;

    protected IReadOnlyList<HistoricParticipation> Rows => _rows;
    protected bool HasMore { get; private set; }
    protected bool IsLoading { get; private set; }
    protected bool IsEmpty => !IsLoading && !HasFailed && _rows.Count == 0;

    /// <summary>Whether the page that was asked for last could not be read.</summary>
    protected bool HasFailed { get; private set; }

    /// <summary>The other half of a Participation is the Horse in the list of an Athlete, and the Athlete in the list of a Horse.</summary>
    protected string OtherHeading => Kind == ParticipantKind.Athlete ? Horse_string : Athlete_string;

    [Parameter, EditorRequired]
    public ParticipantKind Kind { get; set; }

    [Parameter, EditorRequired]
    public Guid Id { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (_shown == (Kind, Id))
        {
            return;
        }

        _shown = (Kind, Id);
        _turn++;
        _rows = [];
        _open.Clear();
        _read.Clear();
        _pages = 0;
        HasMore = false;
        HasFailed = false;
        IsLoading = false; // a page that was being read for the one shown before is not waited for
        await ShowMore();
    }

    protected async Task ShowMore()
    {
        if (IsLoading || _shown is not { } shown)
        {
            return;
        }

        var turn = _turn;
        IsLoading = true;
        try
        {
            var page = await History.HistoryOf(shown.Kind, shown.Id, _pages + 1, PAGE_SIZE, CancellationToken.None);
            if (turn != _turn)
            {
                return; // another has been chosen meanwhile, or the same again
            }

            _pages++;
            _rows = [.. _rows, .. page.Items];
            HasMore = page.HasMore;
            HasFailed = false;
        }
        catch (Exception ex)
        {
            HasFailed = turn == _turn;
            Handle(ex);
        }
        finally
        {
            if (turn == _turn)
            {
                IsLoading = false;
            }
        }
    }

    protected bool IsOpen(HistoricParticipation row)
    {
        return _open.Contains(row.Id);
    }

    /// <summary>Whether the row is open and its Participation is not read yet.</summary>
    protected bool IsReading(HistoricParticipation row)
    {
        return _open.Contains(row.Id) && !_read.ContainsKey(row.Id);
    }

    protected Participation? ReadOf(HistoricParticipation row)
    {
        return _read.GetValueOrDefault(row.Id);
    }

    /// <summary>Opens the row, reading its Participation the first time, or closes it.</summary>
    protected async Task Toggle(TableRowClickEventArgs<HistoricParticipation> args)
    {
        var row = args.Item;
        try
        {
            if (row == null)
            {
                return;
            }

            if (!_open.Add(row.Id))
            {
                _open.Remove(row.Id);
                return;
            }

            if (_read.ContainsKey(row.Id))
            {
                return;
            }

            var turn = _turn;
            var read = await History.ParticipationOf(row.Id);
            if (turn != _turn)
            {
                return;
            }

            if (read.Failed)
            {
                _open.Remove(row.Id); // the person was told when it was read, and opening the row asks again
                return;
            }

            _read[row.Id] = read.Row;
        }
        catch (Exception ex)
        {
            if (row != null)
            {
                _open.Remove(row.Id);
            }

            Handle(ex);
        }
    }

    /// <summary>What a person who cannot see the row is told of the button that opens it.</summary>
    protected static string LabelOf(HistoricParticipation row)
    {
        return $"{DayOf(row)} {row.Competition}";
    }

    protected string IconOf(HistoricParticipation row)
    {
        return IsOpen(row) ? Icons.Material.Filled.ExpandLess : Icons.Material.Filled.ExpandMore;
    }

    /// <summary>The day the Event starts on, as the day of its place (<see cref="EventDay"/>), in the format of the language.</summary>
    protected static string DayOf(HistoricParticipation row)
    {
        return EventDay.Of(row.StartDay).ToString("d", CultureInfo.CurrentCulture);
    }
}
