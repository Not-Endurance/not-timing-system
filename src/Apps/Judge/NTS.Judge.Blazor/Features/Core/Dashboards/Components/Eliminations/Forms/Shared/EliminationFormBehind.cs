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

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected async Task OnRestoreSafe()
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

    protected async Task OnSubmitSafe()
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
