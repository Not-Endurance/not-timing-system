using NTS.Contracts.Features.Access;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// What the Witness says of the person at it, as the tests read it: whether it knows who is signed in, whether somebody is,
/// and whether they may send Snapshots to the Event they follow (the flags of <see cref="IWitnessAccessContext"/>).
/// </summary>
internal readonly record struct WitnessSays(bool IsKnown, bool IsSignedIn, bool CanSendSnapshots)
{
    /// <summary>The host has not said who is signed in.</summary>
    public static WitnessSays Nothing { get; } = new(false, false, false);

    /// <summary>The host has said that nobody is.</summary>
    public static WitnessSays Visitor { get; } = new(true, false, false);

    /// <summary>Somebody is signed in, and may not send Snapshots to the Event they follow.</summary>
    public static WitnessSays Registered { get; } = new(true, true, false);

    /// <summary>Somebody is signed in, and may send Snapshots to the Event they follow.</summary>
    public static WitnessSays Official { get; } = new(true, true, true);

    public static WitnessSays Of(IWitnessAccessContext access)
    {
        return new WitnessSays(access.IsKnown, access.IsSignedIn, access.CanSendSnapshots);
    }
}
