using Not.Blazor.Dialogs.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>
/// The Console Operators of an Event (#671, ADR-0012): who they are as the Api tells it (an address is partly masked and
/// never given whole), whether the invitation still waits for the person to register, a way to end the access of one and a
/// way to assign one more by finding the account by name. Only Root assigns and removes, which the Api decides, and what it
/// refuses is said as a warning.
/// </summary>
public class ConsoleOperatorsDialogBehind : NDialog
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected IReadOnlyList<ConsoleOperatorRow> Rows { get; private set; } = [];
    protected bool IsLoaded { get; private set; }
    protected AccountMatch? Chosen { get; set; }

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    protected static string PersonText(ConsoleOperatorRow row)
    {
        return string.IsNullOrWhiteSpace(row.DisplayName) ? row.Email : $"{row.DisplayName} ({row.Email})";
    }

    protected override async Task OnInitializedAsync()
    {
        await Load();
    }

    protected async Task<IEnumerable<AccountMatch?>> Search(string term, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return term.Trim().Length < 3 ? [] : (await Events.FindAccounts(term.Trim())).Cast<AccountMatch?>();
    }

    protected async Task Assign()
    {
        if (Chosen is not { } chosen)
        {
            return;
        }

        await Safely(async () =>
        {
            await Events.AssignConsoleOperator(EventId, chosen.Id);
            Chosen = null;
            Notifier.Success(Console_operator_assigned_string);
            await Load();
        });
    }

    protected async Task Remove(ConsoleOperatorRow row)
    {
        await Safely(async () =>
        {
            await Events.RemoveConsoleOperator(row.Id);
            Notifier.Success(Console_operator_removed_string);
            await Load();
        });
    }

    async Task Safely(Func<Task> act)
    {
        try
        {
            await act();
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task Load()
    {
        await Safely(async () => Rows = await Events.ConsoleOperators(EventId));
        IsLoaded = true;
        await InvokeRender();
    }
}
