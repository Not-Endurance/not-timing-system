using Not.Blazor.Components;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Judge.Blazor.Core.Startlists;

public abstract class StartlistTabs : NComponent
{
    protected Dictionary<string, List<StartlistEntry>> StartlistsByStage { get; set; } = [];

    protected virtual string[] TableHeaders { get; } = ["Number", "Athlete", "Loop", "Start Time"];

    protected void CreateStartlistsByStage(IEnumerable<StartlistEntry> starts)
    {
        foreach (var start in starts)
        {
            var tabHeader = $"{Stage_string} {start.PhaseNumber}";
            if (!StartlistsByStage.Keys.Any(t => t == tabHeader))
            {
                StartlistsByStage.Add(tabHeader, []);
            }
            if (!StartlistsByStage[tabHeader].Any(s => s.Number == start.Number))
            {
                StartlistsByStage[tabHeader].Add(start);
            }
        }
    }
}
