using Not.Blazor.Components.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Dashboard.Eliminations.Forms;

/// <summary>
/// What every way of eliminating a Participation shares (#646): the button that eliminates or updates the elimination, and the
/// one that restores the qualification. What the Api does not take is said to the person, in its words, and nothing is lost.
/// </summary>
public class EliminationFormBehind : NComponent
{
    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected bool IsEliminated => Dashboard.IsEliminated;

    [Parameter, EditorRequired]
    public Func<Task> SubmitSafe { get; set; } = default!;

    protected Task OnRestore()
    {
        return Act(Dashboard.RestoreQualification);
    }

    protected Task OnSubmit()
    {
        return Act(SubmitSafe);
    }

    async Task Act(Func<Task> action)
    {
        try
        {
            await action();
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
}
