using Not.Blazor.Dialogs.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>Finds an account of the Tenant by name, to be the Main Operator of an Event: assigned before it is Live, handed over once it is.</summary>
public class MainOperatorDialogBehind : NDialog<AccountMatch>
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    protected AccountMatch? Chosen { get; set; }

    protected async Task<IEnumerable<AccountMatch?>> Search(string term, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return term.Trim().Length < 3 ? [] : (await Events.FindAccounts(term.Trim())).Cast<AccountMatch?>();
    }

    protected Task Choose()
    {
        return Chosen == null ? Task.CompletedTask : ConfirmDialog(Chosen);
    }
}
