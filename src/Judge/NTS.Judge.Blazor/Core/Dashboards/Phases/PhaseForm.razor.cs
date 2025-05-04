using MudBlazor;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Blazor;

namespace NTS.Judge.Blazor.Core.Dashboards.Phases;

public partial class PhaseForm
{
    static readonly PatternMask TIME_MASK = new(Constants.SECONDS_TIME_MASK_FORMAT);

    MudTextField<string?> _startField = default!;
    MudTextField<string?> _arriveField = default!;
    MudTextField<string?> _presentField = default!;
    MudTextField<string?> _representField = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Phase.StartTime), () => _startField);
        RegisterInjector(nameof(Phase.ArriveTime), () => _arriveField);
        RegisterInjector(nameof(Phase.PresentTime), () => _presentField);
        RegisterInjector(nameof(Phase.RepresentTime), () => _representField);
    }
}
