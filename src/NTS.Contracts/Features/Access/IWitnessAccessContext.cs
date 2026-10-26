using Not.Application.Behinds.Adapters;

namespace NTS.Contracts.Features.Access;

/// <summary>
/// What the host and the Api say the person at this Witness may do about the Event it follows (ADR-0012, #671): flags that
/// are told, never a level that the app works out for itself.
/// </summary>
public interface IWitnessAccessContext : IStatefulService
{
    /// <summary>Whether the host has said who is signed in. Until it has, nothing is offered to the person or withheld from them.</summary>
    bool IsKnown { get; }

    /// <summary>Whether somebody is signed in at this Witness, as the host says.</summary>
    bool IsSignedIn { get; }

    /// <summary>Whether the Api lets the person send a Snapshot to the Event they follow: the <c>canSnapshot</c> of its capabilities.</summary>
    bool CanSendSnapshots { get; }
}
