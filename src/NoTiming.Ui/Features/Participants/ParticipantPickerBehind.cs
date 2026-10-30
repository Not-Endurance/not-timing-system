using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Participants;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// An input that finds an Athlete, or a Horse, by a few letters of its name (#639, ADR-0010). The Api searches the registry of
/// the kind for a word of at least two characters and counts each search against the budget of the account, so the input asks
/// after a pause and not at each key. It offers the first page of what is found, ten at most, and a person who wants another
/// goes on typing.
/// </summary>
public class ParticipantPickerBehind : NComponent
{
    [Inject]
    IParticipantSearch Participants { get; set; } = default!;

    protected ParticipantName? Chosen { get; private set; }

    /// <summary>Changes when an input that adds is to be empty again, which is a new input: the one that was used keeps its text.</summary>
    protected int Generation { get; private set; }

    protected static int MinCharacters => ParticipantService.MIN_CHARACTERS;

    protected static int PauseMilliseconds => ParticipantService.SEARCH_PAUSE_MILLISECONDS;

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? HelperText { get; set; }

    /// <summary>The kind this picker offers: the Athletes, or the Horses.</summary>
    [Parameter, EditorRequired]
    public ParticipantKind Kind { get; set; }

    /// <summary>Whether the input is emptied once a Participant is chosen, as an input that adds does; otherwise it keeps its name.</summary>
    [Parameter]
    public bool ClearOnChosen { get; set; }

    /// <summary>The Participant that was chosen, or none when the input was cleared.</summary>
    [Parameter]
    public EventCallback<ParticipantName?> OnChosen { get; set; }

    protected async Task<IEnumerable<ParticipantName?>> FindSafe(string term, CancellationToken ct)
    {
        try
        {
            var found = await Participants.Search(Kind, term, 1, ct);
            return found.Items.Cast<ParticipantName?>();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return []; // the person typed on, and the answer to what they typed before is not wanted
        }
    }

    protected async Task Choose(ParticipantName? participant)
    {
        try
        {
            Chosen = ClearOnChosen ? null : participant;
            if (ClearOnChosen && participant != null)
            {
                Generation++;
            }

            await OnChosen.InvokeAsync(participant);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>The input was emptied with its button: nobody is chosen.</summary>
    protected async Task Clear()
    {
        try
        {
            Chosen = null;
            await OnChosen.InvokeAsync(null);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
