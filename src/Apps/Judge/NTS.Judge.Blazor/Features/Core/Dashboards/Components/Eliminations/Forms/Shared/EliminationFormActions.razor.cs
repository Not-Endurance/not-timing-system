namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms.Shared;

public partial class EliminationFormActions
{
    [Parameter, EditorRequired]
    public EliminationForm Form { get; set; } = default!;
}
