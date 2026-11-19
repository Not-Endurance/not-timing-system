using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects.Payloads;

namespace NoTiming.Domain.Core.Events;

/// <summary>
/// A participation changed, with its current state and the state before the change when this client knows it.
/// IsRemote is false for changes this client made itself.
/// </summary>
public record ParticipationChanged : ParticipationPayload
{
    public ParticipationChanged(Participation participation, Participation? previous, bool isRemote)
        : base(participation)
    {
        Previous = previous;
        IsRemote = isRemote;
    }

    public Participation? Previous { get; }

    public bool IsRemote { get; }
}
