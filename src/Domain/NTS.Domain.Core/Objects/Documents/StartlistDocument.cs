using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents.Base;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Domain.Core.Objects.Documents;

/// <summary>
/// The Startlist of an Event as a sheet to hand out (#646): every start that has a time, of every Phase of every Participation,
/// by stage (the Phase), in the order they start. A start has a time when its Phase has one, or when the Phase before it is
/// complete and so out, so the sheet grows as the Event goes on. The Starters are what <see cref="Startlist"/> makes of the
/// Participations, with the starts that are over and the ones to come in one list: what is printed is the whole of it.
/// </summary>
public sealed record StartlistDocument : Document
{
    /// <summary>The sheet of the Participations of an Event, headed by the Event; its Ruleset is the one of the first Participation.</summary>
    public static StartlistDocument Compose(
        IEnumerable<Participation> participations,
        EventInformation eventInformation,
        IEnumerable<Official> officials
    )
    {
        var all = participations.OrderBy(x => x.Combination.Number).ToList();
        var startlist = new Startlist(all);
        var stages = startlist
            .History.Concat(startlist.Upcoming)
            .GroupBy(x => x.PhaseNumber)
            .OrderBy(x => x.Key)
            .Select(x => new StartlistStage(
                x.Key,
                [.. x.OrderBy(starter => starter.Start).ThenBy(starter => starter.Number)]
            ))
            .ToList();
        var ruleset = all.Count == 0 ? CompetitionRuleset.FEI : all[0].Competition.Ruleset;
        var header = new DocumentHeader(
            eventInformation.Name,
            ruleset,
            eventInformation.Country,
            eventInformation.Location,
            eventInformation.EventSpan,
            officials
        );
        return new StartlistDocument(header, stages);
    }

    StartlistDocument(DocumentHeader header, IReadOnlyList<StartlistStage> stages)
        : base(header)
    {
        Stages = stages;
    }

    public IReadOnlyList<StartlistStage> Stages { get; }
}
