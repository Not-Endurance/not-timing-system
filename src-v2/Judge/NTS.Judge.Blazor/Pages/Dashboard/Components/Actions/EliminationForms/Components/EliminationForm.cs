using NTS.Judge.Blazor.Ports;
using Not.Blazor.Components;

namespace NTS.Judge.Blazor.Pages.Dashboard.Components.Actions.EliminationForms;

public abstract class EliminationForm : NotComponent
{
    [Inject]
    protected IParticipationBehind _participationBehind { get; set; } = default!;

    public bool IsEliminated => _participationBehind.SelectedParticipation?.Eliminated != null;

    internal abstract Task Eliminate();

    internal async Task Restore()
    {
        await _participationBehind.RestoreQualification();
    }
}

