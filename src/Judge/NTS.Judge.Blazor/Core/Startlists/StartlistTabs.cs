using Not.Blazor.Components;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Judge.Blazor.Core.Startlists;

public abstract class StartlistTabs : NComponent
{
    protected Dictionary<string, List<StartlistEntry>> StartlistsByStage { get; } = [];

    protected virtual string[] TableHeaders { get; } = [Number_string, Athlete_string, Loops_string, Start_Time_string];

    protected void CreateStartlistsByStage(IEnumerable<StartlistEntry> starts)
    {
        foreach (var start in starts)
        {
            var tabHeader = $"{Stage_string} {start.PhaseNumber}";
            if (StartlistsByStage.Keys.All(t => t != tabHeader))
            {
                StartlistsByStage.Add(tabHeader, []);
            }
            if (StartlistsByStage[tabHeader].All(s => s.Number != start.Number))
            {
                StartlistsByStage[tabHeader].Add(start);
            }
        }
    }
}
