namespace NTS.Contracts.Features.Console;

/// <summary>
/// The timeline of the Participation the Main Operator works on in the Console (#619, ADR-0005): every time event of every
/// Phase, in the order they were recorded, with what became of it, how it was acquired, who recorded it and when, which is
/// what a Participation tells only to Staff. The people are named here and nowhere else. What is done with the events is the
/// dashboard's: the chosen ones are disabled and enabled by its one change, which is written to the Api against the version
/// of the Participation that the person saw.
/// </summary>
public interface IConsoleTimeline
{
    /// <summary>
    /// The timeline of the selected Participation as it is stored now, by Phase, each Phase with the events it recorded in
    /// the order it recorded them. The names of the people are asked of the Api each time, because who has recorded a time
    /// since is part of what the person looks at.
    /// </summary>
    Task<IReadOnlyList<TimelinePhase>> Read();
}
