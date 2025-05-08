using Microsoft.AspNetCore.Components;
using Not.Blazor.Dialogs;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Blazor.Components.ParticipationTable.Phases;

namespace NTS.Blazor.Components.ParticipationTable;

public partial class ParticipationTable
{
    const string FLIPPED_AXIS_CLASS = "rtable--flip";

    string _axisClass = FLIPPED_AXIS_CLASS;
    bool _anyRepresentation;
    bool _anyRequiredInspection;
    bool _anyCompulsoryRequiredInspection;

    [Inject]
    Dialog<PhaseUpdateModel, PhaseForm> Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public int Number { get; set; }

    [Parameter]
    public PhaseCollection? Phases { get; set; }

    protected override void OnParametersSet()
    {
        _anyRepresentation = Phases != null && Phases.Any(x => x.IsReinspectionRequested);
        _anyRequiredInspection = Phases != null && Phases.Any(x => x.IsRequiredInspectionRequested);
        _anyCompulsoryRequiredInspection = Phases != null && Phases.Any(x => x.IsRequiredInspectionCompulsory);
    }

#pragma warning disable IDE0051 // Remove unused private members
    void FlipAxis()
#pragma warning restore IDE0051 // Remove unused private members
    {
        if (_axisClass == FLIPPED_AXIS_CLASS)
        {
            _axisClass = "";
        }
        else
        {
            _axisClass = FLIPPED_AXIS_CLASS;
        }
    }

    async Task ShowUpdate(Phase phase)
    {
        var model = new PhaseUpdateModel(phase);
        await Dialog.RenderUpdate(model);
        await Render();
    }
}
