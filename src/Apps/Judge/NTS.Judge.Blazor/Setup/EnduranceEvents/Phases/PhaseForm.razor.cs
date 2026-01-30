using MudBlazor;
using Not.Application.Services;
using Not.Blazor.Components;
using Not.Structures;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

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
        var loops = await Behind.ReadMany();
        _loops = NotListModel.FromEntity(loops).ToList();
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Phase.Loop), () => _loopField);
        RegisterInjector(nameof(Phase.Recovery), () => _recoveryField);
        RegisterInjector(nameof(Phase.Rest), () => _restField);
    }
}
