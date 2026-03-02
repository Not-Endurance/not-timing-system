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
        await Eliminations.RestoreQualification();
    }

    protected async Task OnSubmitSafe()
    {
        await SubmitSafe();
    }
}
