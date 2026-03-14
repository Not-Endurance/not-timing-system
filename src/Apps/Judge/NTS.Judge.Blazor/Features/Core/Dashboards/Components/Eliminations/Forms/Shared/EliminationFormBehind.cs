using Not.Blazor.Components.Abstractions;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms.Shared;

public abstract class EliminationFormBehind : NComponent
{
    [Inject]
    IEliminationService Eliminations { get; set; } = default!;

    protected bool IsEliminated => Eliminations.IsEliminated;

    [Parameter, EditorRequired]
    public Func<Task> SubmitSafe { get; set; } = default!;

    protected async Task OnRestore()
    {
        try
        {
            await Eliminations.RestoreQualification();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OnSubmit()
    {
        try
        {
            await SubmitSafe();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
