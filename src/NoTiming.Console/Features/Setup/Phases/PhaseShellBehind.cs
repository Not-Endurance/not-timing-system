using Not.Krud.Blazor.Components.Abstractions;
using Not.Structures;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Console.Features.Setup.Phases;

public class PhaseShellBehind : KrudShell<PhaseFormModel>
{
    [Inject]
    IConsoleLookups Lookups { get; set; } = default!;

    protected IEnumerable<NotListModel<Loop>> Loops { get; private set; } = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Loops = NotListModel.FromEntity(await Lookups.GetLoops());
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
