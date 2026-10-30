namespace NTS.Contracts.Core;

/// <summary>
/// The time of the server as the Ui knows it (#646). A request for a Representation or an Inspection is placed in a Phase by
/// the time it is made, and that is the clock of the server, which a device agrees with to within about a minute and not more
/// (the glossary's Phase): the device asks the server what time it is, and keeps the difference between the two clocks.
/// </summary>
public interface IServerClock
{
    /// <summary>The time of the server now: the clock of the device and the difference that was last measured.</summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Measures the difference between the clock of the device and the one of the server, from the time the server says in its
    /// answer. A server that does not say, or cannot be reached, leaves the clock of the device as it is.
    /// </summary>
    Task Synchronise();
}
