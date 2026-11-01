using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Participants;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// The Athletes, or the Horses, that a text finds, as a list of names a person chooses from, a page of ten at a time with
/// "Show more" (#639, ADR-0010). The Athletes and the Horses are two registries and so two lists, each of its own pages. A list is
/// read when the text changes to one that is searched for, and what was asked for before another text is never shown after it.
/// </summary>
public class ParticipantMatchesBehind : NComponent
{
    List<ParticipantName> _items = [];
    string? _searchedFor;
    int _turn; // changes with the text that is searched for, so that what was asked for one is not taken for what is asked for the next
    int _pages;

    [Inject]
    IParticipantSearch Participants { get; set; } = default!;

    protected IReadOnlyList<ParticipantName> Items => _items;
    protected bool HasMore { get; private set; }
    protected bool IsLoading { get; private set; }

    /// <summary>Whether the page that was asked for last could not be read.</summary>
    protected bool HasFailed { get; private set; }

    /// <summary>Whether the text is one that is searched for, and so whether there is a list at all.</summary>
    protected bool IsSearched => ParticipantService.IsSearchable(Text);

    protected bool IsEmpty => IsSearched && !IsLoading && !HasFailed && _items.Count == 0;

    protected string Heading => Kind == ParticipantKind.Athlete ? Athletes_string : Horses_string;

    [Parameter, EditorRequired]
    public ParticipantKind Kind { get; set; }

    /// <summary>What the person has typed.</summary>
    [Parameter]
    public string? Text { get; set; }

    /// <summary>The Participant that was chosen from the list.</summary>
    [Parameter]
    public EventCallback<ParticipantName> OnChosen { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var text = ParticipantService.IsSearchable(Text) ? Text!.Trim() : null;
        if (text == _searchedFor)
        {
            return;
        }

        _searchedFor = text;
        _turn++;
        _items = [];
        _pages = 0;
        HasMore = false;
        HasFailed = false;
        IsLoading = false; // a page that was being read for the text before is not waited for
        await ShowMore();
    }

    protected async Task ShowMore()
    {
        if (IsLoading || _searchedFor is not { } text)
        {
            return;
        }

        var turn = _turn;
        IsLoading = true;
        try
        {
            var page = await Participants.Search(Kind, text, _pages + 1, CancellationToken.None);
            if (turn != _turn)
            {
                return; // the person has typed on meanwhile
            }

            _pages++;
            _items = [.. _items, .. page.Items];
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

    protected async Task Choose(ParticipantName participant)
    {
        try
        {
            await OnChosen.InvokeAsync(participant);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
