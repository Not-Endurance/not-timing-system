using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Dashboard.Eliminations.Forms;

public class WithdrawFormBehind : NComponent
{
    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    protected Task WithdrawSafe()
    {
        return Dashboard.Withdraw();
    }
}
