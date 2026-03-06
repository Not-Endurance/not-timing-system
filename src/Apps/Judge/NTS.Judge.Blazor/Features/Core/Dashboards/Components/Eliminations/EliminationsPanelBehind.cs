using Not.Blazor.Components.Abstractions;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations;

public class EliminationsPanelBehind : NStatefulComponent
{
    string? _inputValue;

    protected Eliminated? Eliminated => ParticipationContext.Selected?.Eliminated;
    protected string? ToggleValue
    {
        get => _inputValue != null ? _inputValue : Eliminated?.Code;
        set => _inputValue = value;
    }

    [Inject]
    protected IParticipationContext ParticipationContext { get; set; } = default!;

    protected string? Reason { get; set; }

    protected IEnumerable<DisqualifyCode> DisqualificationCodes { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        await Observe(ParticipationContext);
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
