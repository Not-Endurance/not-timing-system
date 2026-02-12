using Not.Blazor.Components;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations;

public class EliminationsPanelBehind : NStatefulComponent
{
    string? _inputValue;

    protected Eliminated? Eliminated => Service.Selected?.Eliminated;
    protected string? ToggleValue
    {
        get => _inputValue != null ? _inputValue : Eliminated?.Code;
        set => _inputValue = value;
    }

    [Inject]
    protected IParticipationContext Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
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
