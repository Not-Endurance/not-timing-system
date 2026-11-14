using MudBlazor;
using NC.Krud.Blazor.Components.Abstractions;
using NoTiming.Blazor.Constants;
using NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Competitions;

namespace NoTiming.Judge.Blazor.Features.Setup.ConfigureEvents.Competitions;

public class CompetitionShellBehind : KrudShell<CompetitionFormModel>
{
    public CompetitionShellBehind()
    {
        TimeMask = new(Masks.MINUTES_TIME_MASK_FORMAT);
    }

    protected PatternMask TimeMask { get; }
}
