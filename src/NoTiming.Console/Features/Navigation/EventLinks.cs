using NTS.Contracts.Features.Console;
using NTS.Domain.Access;

namespace NoTiming.Console.Features.Navigation;

/// <summary>
/// The pages of an Event that the Console offers a person (#646), which are the ones that the Event at its stage has for them
/// and no others: the Setup of an Event that has not started, the dashboard, the Startlist, the Results and the Handouts of a
/// Live Event that the person runs, with the Arrivelist and the Presentlist that the viewers have, and the Results of any
/// other that the Console lists (a Historic one, or a Live one that a Tenant Root has assigned away). The Console itself is
/// always the first.
/// </summary>
public static class EventLinks
{
    /// <param name="row">The Event as the Api tells the person of it.</param>
    /// <param name="current">The address template of the page the person is on, as the page declares it.</param>
    public static IReadOnlyList<EventLink> For(ConsoleEvent row, string current)
    {
        var links = new List<EventLink> { new(Console_string, ConsoleRoutes.HOME, current == ConsoleRoutes.HOME) };
        foreach (var (text, template) in PagesOf(row))
        {
            links.Add(new EventLink(text, Routes.Of(template, row.Id), template == current));
        }

        return links;
    }

    static IEnumerable<(string Text, string Template)> PagesOf(ConsoleEvent row)
    {
        if (row is { Stage: EventStage.Unstarted, IsMainOperator: true })
        {
            yield return (Setup_string, ConsoleRoutes.EVENT_SETUP_PAGE);
            yield return (Logos_string, ConsoleRoutes.EVENT_BRANDING_PAGE);
        }
        else if (row is { Stage: EventStage.Live, IsMainOperator: true })
        {
            yield return (Dashboard_string, ConsoleRoutes.EVENT_DASHBOARD_PAGE);
            yield return (Startlist_string, ConsoleRoutes.EVENT_STARTLIST_PAGE);
            yield return (Ranklist_string, ConsoleRoutes.EVENT_RANKINGS_PAGE);
            yield return (Handouts_string, ConsoleRoutes.EVENT_HANDOUTS_PAGE);
            yield return (Logos_string, ConsoleRoutes.EVENT_BRANDING_PAGE);
            yield return (Arrivelist_string, Routes.EVENT_ARRIVELIST_PAGE);
            yield return (Presentlist_string, Routes.EVENT_PRESENTLIST_PAGE);
        }
        else if (row.Stage != EventStage.Unstarted)
        {
            yield return (Ranklist_string, ConsoleRoutes.EVENT_RANKINGS_PAGE);
        }
    }
}
