using Not.Blazor.Components;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms.Shared;

public abstract class EliminationForm : NComponent
{
    internal abstract Task Eliminate();

    [Inject]
    protected IEliminations Eliminations { get; set; } = default!;
    public bool IsEliminated => Eliminations.SelectedParticipation?.Eliminated != null;

    internal async Task Restore()
    {
        await Eliminations.RestoreQualification();
    }
}
