using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Structures;
using NTS.Blazor.Constants;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Competitions;

namespace NTS.Judge.Blazor.Features.Setup.ConfigureEvents.Competitions;

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
