using Not.Blazor.Dialogs.Abstractions;
using NTS.Contracts.Features.Participants;

namespace NoTiming.Ui.Features.Participants;

/// <summary>The Participations of one Athlete or Horse in a dialog (#639), opened from a list of Participants such as the Horses of a profile.</summary>
public class ParticipantDialogBehind : NDialog
{
    [Parameter, EditorRequired]
    public ParticipantName Participant { get; set; } = default!;
}
