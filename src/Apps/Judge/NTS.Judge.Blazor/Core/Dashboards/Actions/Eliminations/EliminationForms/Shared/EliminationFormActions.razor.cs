namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms.Shared;

public partial class EliminationFormActions
{
    [Parameter, EditorRequired]
    public EliminationForm Form { get; set; } = default!;
}
