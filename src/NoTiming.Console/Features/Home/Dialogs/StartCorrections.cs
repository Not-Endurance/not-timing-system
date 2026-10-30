using NTS.Domain.Setup.Services.StartValidation;

namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>
/// What the person may settle about the start numbers that two Competitions of different Phases hold at once (#646): for each of
/// them they choose the Competition to take it out of, as the choice is worded, and what is left is validated again. Judge took it
/// out of the others than the one that was chosen, under the same words; the words are what the person reads.
/// </summary>
public static class StartCorrections
{
    /// <summary>Whether there is something to settle, a choice settles every issue there is, and the person has made it for each.</summary>
    public static bool CanApply(IReadOnlyList<StartValidationIssue> issues, IReadOnlyDictionary<int, Guid> removeFrom)
    {
        return issues.Count > 0
            && issues.All(x =>
                x.IsAutoCorrectable
                && x.ParticipationNumber.HasValue
                && removeFrom.ContainsKey(x.ParticipationNumber.Value)
            );
    }

    /// <summary>The Participation of each start number that is chosen, taken out of the Competition that was chosen for it.</summary>
    public static IEnumerable<(int Number, Guid CompetitionId)> Removals(
        IReadOnlyList<StartValidationIssue> issues,
        IReadOnlyDictionary<int, Guid> removeFrom
    )
    {
        foreach (var issue in issues)
        {
            var number = issue.ParticipationNumber!.Value;
            yield return (number, removeFrom[number]);
        }
    }
}
