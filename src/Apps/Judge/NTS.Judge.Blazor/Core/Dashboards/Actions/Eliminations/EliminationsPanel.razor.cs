using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations;

public partial class EliminationsPanel
{
    string? _inputValue;

    [Inject]
    IParticipationContext ParticipationContext { get; set; } = default!;

    Eliminated? Eliminated => ParticipationContext.SelectedParticipation?.Eliminated;
    string? ToggleValue
    {
        get => _inputValue != null ? _inputValue : Eliminated?.Code;
        set => _inputValue = value;
    }

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
