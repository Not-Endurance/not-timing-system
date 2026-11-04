using MudBlazor;
using NC.Krud.Blazor.Components.Abstractions;
using NC.Structures;
using NoTiming.Blazor.Constants;
using NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Competitions;

namespace NoTiming.Judge.Blazor.Features.Setup.ConfigureEvents.Competitions;

public class CompetitionShellBehind : KrudShell<CompetitionFormModel>
{
    public CompetitionShellBehind()
    {
        TimeMask = new(Masks.MINUTES_TIME_MASK_FORMAT);
        Rules = NotListModel.FromEnum<CompetitionRuleset>().ToList();
    }

    protected PatternMask TimeMask { get; }

    protected List<NotListModel<CompetitionRuleset>> Rules { get; }
}
