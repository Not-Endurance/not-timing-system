using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Structures;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;
using NTS.Judge.Features.Setup.UpcomingEvents.Phases;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents.Phases;

public class PhaseShellBehind : KrudShell<PhaseFormModel>
{
    [Inject]
    IListBehind<Loop> Behind { get; set; } = default!;

    protected IEnumerable<NotListModel<Loop>> Loops { get; private set; } = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var loops = await Behind.ReadMany();
            Loops = NotListModel.FromEntity(loops);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
