using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Dashboard.Eliminations.Forms;

public class RetireFormBehind : NComponent
{
    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    protected Task RetireSafe()
    {
        return Dashboard.Retire();
    }
}
