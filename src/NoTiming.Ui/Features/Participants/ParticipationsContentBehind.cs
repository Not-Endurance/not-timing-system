using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Participants;

namespace NoTiming.Ui.Features.Participants;

/// <summary>The search for an Athlete or a Horse and the Participations of the one chosen (#639, ADR-0010), for a person who is signed in.</summary>
public class ParticipationsContentBehind : NComponent
{
    protected ParticipantName? Chosen { get; private set; }

    protected string KindText => Chosen?.Kind == ParticipantKind.Horse ? Horse_string : Athlete_string;

    protected void Choose(ParticipantName? participant)
    {
        Chosen = participant;
    }
}
