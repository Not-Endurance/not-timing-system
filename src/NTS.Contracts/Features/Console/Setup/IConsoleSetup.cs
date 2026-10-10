namespace NTS.Contracts.Features.Console.Setup;

/// <summary>
/// The Setup of an Event as the Console edits it (#646): opened by the Main Operator of the Event, edited until the Event
/// starts, one form for the Event and one for each Competition, Loop, Combination and Official of it. What the Api refuses
/// (an Event that has started, a person who does not run it) comes back as a domain error in the Api's own words.
/// </summary>
public interface IConsoleSetup
{
    /// <summary>
    /// Opens the Setup of the Event for the person: it is read from the Api and made the one that the pages of the Setup edit.
    /// None when the Event is not there for the person or the person does not run it: a Tenant Root who assigned the role
    /// away, an Operator, an Official and the Developer open nothing.
    /// </summary>
    Task<OpenedSetup?> Open(Guid eventId);

    /// <summary>
    /// Takes a Participation out of one Competition of the Setup, which is how a start number that two Competitions hold at
    /// once is corrected: the Competition it stays in is the person's choice.
    /// </summary>
    Task RemoveParticipation(Guid eventId, int participationNumber, Guid competitionId);
}
