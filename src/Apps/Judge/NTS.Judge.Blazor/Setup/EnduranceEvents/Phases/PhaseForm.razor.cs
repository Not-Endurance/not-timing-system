using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Structures;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.EnduranceEvents.Phases;

public partial class PhaseForm
{
    NSelect<Loop> _loopField = default!;
    MudNumericField<int?> _recoveryField = default!;
    MudNumericField<int?> _restField = default!;
    List<NotListModel<Loop>> _loops = [];

    [Inject]
    IListBehind<Loop> Behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
        _loops = NotListModel.FromEntity<Loop>(Behind.Items).ToList();
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Phase.Loop), () => _loopField);
        RegisterInjector(nameof(Phase.Recovery), () => _recoveryField);
        RegisterInjector(nameof(Phase.Rest), () => _restField);
    }
}
