using NC.Krud.Blazor.Components.Abstractions;
using NC.Structures;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;
using NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Phases;

namespace NoTiming.Judge.Blazor.Features.Setup.ConfigureEvents.Phases;

public class PhaseShellBehind : KrudShell<PhaseFormModel>
{
    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected IEnumerable<NotListModel<Loop>> Loops { get; private set; } = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var loops = await Lookups.GetLoops(CancellationToken.None);
            Loops = NotListModel.FromEntity(loops);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
