using Not.Blazor.Components;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Actions.Eliminations;

public class EliminationsPanelBehind : NStatefulComponent<IParticipationContext>
{
    string? _inputValue;

    protected Eliminated? Eliminated => Service.SelectedParticipation?.Eliminated;
    protected string? ToggleValue
    {
        get => _inputValue != null ? _inputValue : Eliminated?.Code;
        set => _inputValue = value;
    }

    protected override void OnBeforeRender()
    {
        ResetInput();
    }

    void ResetInput()
    {
        _inputValue = null;
    }
}
