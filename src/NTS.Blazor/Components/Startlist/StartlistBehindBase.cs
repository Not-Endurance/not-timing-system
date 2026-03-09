using Not.Blazor.Components.Abstractions;
using static NTS.Localization.NtsStrings;

namespace NTS.Blazor.Components.Startlist;

public abstract class StartlistBehindBase : NStatefulComponent
{
    protected virtual string[] TableHeaders { get; } = [Number_string, Athlete_string, Loops_string, Start_Time_string];

    protected string StageHeader(int phaseNumber)
    {
        return $"{Stage_string} {phaseNumber}";
    }
}
