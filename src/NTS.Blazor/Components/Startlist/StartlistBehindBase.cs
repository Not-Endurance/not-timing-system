using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Startlists;
using static NTS.Localization.NtsStrings;

namespace NTS.Blazor.Components.Startlist;

public abstract class StartlistBehindBase : NStatefulComponent
{
    protected Dictionary<string, List<StartlistEntry>> StartlistsByStage { get; } = [];

    protected virtual string[] TableHeaders { get; } = [Number_string, Athlete_string, Loops_string, Start_Time_string];

    protected void CreateStartlistsByStage(IEnumerable<StartlistEntry> starts)
    {
        foreach (var start in starts)
        {
            var tabHeader = $"{Stage_string} {start.PhaseNumber}";
            if (!StartlistsByStage.TryGetValue(tabHeader, out List<StartlistEntry>? value))
            {
                value = [];
                StartlistsByStage.Add(tabHeader, value);
            }
            if (value.All(s => s.Number != start.Number))
            {
                value.Add(start);
            }
        }
    }
}
