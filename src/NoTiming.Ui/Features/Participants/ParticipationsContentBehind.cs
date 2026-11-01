using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Participants;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// The search for an Athlete or a Horse and the Participations of the one chosen (#639, ADR-0010), for a person who is signed
/// in: one input, the Athletes it finds and the Horses it finds as two lists, and the history of the one chosen under them.
/// </summary>
public class ParticipationsContentBehind : NComponent
{
    protected string? Text { get; private set; }

    protected ParticipantName? Chosen { get; private set; }

    protected string KindText => Chosen?.Kind == ParticipantKind.Horse ? Horse_string : Athlete_string;

    protected static int PauseMilliseconds => ParticipantService.SEARCH_PAUSE_MILLISECONDS;

    /// <summary>The person has typed and stopped. A text that is taken away takes the Participant that was chosen with it.</summary>
    protected void TextChanged(string? text)
    {
        try
        {
            Text = text;
            if (string.IsNullOrWhiteSpace(text))
            {
                Chosen = null;
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void Choose(ParticipantName participant)
    {
        try
        {
            Chosen = participant;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
