using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using NoTiming.Ui.Constants;

namespace NoTiming.Console.Features.Setup.Competitions;

public class CompetitionShellBehind : KrudShell<CompetitionFormModel>
{
    public CompetitionShellBehind()
    {
        TimeMask = new(Masks.MINUTES_TIME_MASK_FORMAT);
    }

    protected PatternMask TimeMask { get; }
}
