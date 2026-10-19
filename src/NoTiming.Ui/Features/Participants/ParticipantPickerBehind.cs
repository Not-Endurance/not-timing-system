using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Participants;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// An input that finds an Athlete or a Horse by a few letters of its name, in either script (#639, ADR-0010). The Api searches
/// the registries of every Tenant for at least three characters and counts each search against the budget of the account, so
/// the input asks after a pause and not at each key. What it finds is told with its kind, and a picker made for one kind shows
/// only the matches of it.
/// </summary>
public class ParticipantPickerBehind : NComponent
{
    public const int DEBOUNCE_MILLISECONDS = 700;

    [Inject]
    IParticipantSearch Participants { get; set; } = default!;

    protected ParticipantName? Chosen { get; private set; }

    /// <summary>Changes when an input that adds is to be empty again, which is a new input: the one that was used keeps its text.</summary>
    protected int Generation { get; private set; }

    protected static int MinCharacters => ParticipantService.MIN_CHARACTERS;

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? HelperText { get; set; }

    /// <summary>The one kind this picker offers; both when none.</summary>
    [Parameter]
    public ParticipantKind? OnlyOf { get; set; }

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
            var found = await Participants.Search(term, ct);
            return found.Where(x => OnlyOf == null || x.Kind == OnlyOf).Cast<ParticipantName?>();
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

    protected static string KindText(ParticipantName participant)
    {
        return participant.Kind == ParticipantKind.Athlete ? Athlete_string : Horse_string;
    }
}
