using Not.Blazor.Components;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms.Shared;

public abstract class EliminationForm : NComponent
{
    internal abstract Task Eliminate();

    [Inject]
    protected IEliminationService Eliminations { get; set; } = default!;
    
    public bool IsEliminated => Eliminations.IsEliminated;

    internal async Task Restore()
    {
        await Eliminations.RestoreQualification();
    }
}
