using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Structures;
using NTS.Blazor.Constants;
using NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionShellbehind : KrudShell<CompetitionFormModel>
{
    public CompetitionShellbehind()
    {
        TimeMask = new(Masks.MINUTES_TIME_MASK_FORMAT);
        Types = NotListModel.FromEnum<CompetitionType>().ToList();
        Rules = NotListModel.FromEnum<CompetitionRuleset>().ToList();
    }

    protected PatternMask TimeMask { get; }

    protected List<NotListModel<CompetitionType>> Types { get; }

    protected List<NotListModel<CompetitionRuleset>> Rules { get; }
}
