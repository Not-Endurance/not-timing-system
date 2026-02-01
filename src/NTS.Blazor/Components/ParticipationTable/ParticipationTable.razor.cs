using Microsoft.AspNetCore.Components;
using Not.Blazor.Dialogs;
using Not.Krud.Blazor;
using NTS.Application.Core;
using NTS.Blazor.Components.ParticipationTable.Phases;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Blazor.Components.ParticipationTable;

public partial class ParticipationTable
{
    const string FLIPPED_AXIS_CLASS = "rtable--flip";

    string _axisClass = FLIPPED_AXIS_CLASS;
    bool _anyRepresentation;
    bool _anyRequiredInspection;
    bool _anyCompulsoryRequiredInspection;

    [Inject]
    FormDialogService<PhaseUpdateModel, PhaseForm> Dialog { get; set; } = default!;

    [Parameter]
    public PhaseCollection? Phases { get; set; }

    [Parameter]
    public bool Editable { get; set; } = true;

    [Parameter]
    public bool AlignVertically { get; set; } = default;

    protected override void OnParametersSet()
    {
        _anyRepresentation = Phases != null && Phases.Any(x => x.IsReinspectionRequested);
        _anyRequiredInspection = Phases != null && Phases.Any(x => x.IsRequiredInspectionRequested);
        _anyCompulsoryRequiredInspection = Phases != null && Phases.Any(x => x.IsRequiredInspectionCompulsory);
        FlipAxis();
    }

#pragma warning disable IDE0051 // Remove unused private members
    void FlipAxis()
#pragma warning restore IDE0051 // Remove unused private members
    {
        if (AlignVertically)
        {
            _axisClass = "size-xs";
        }
        else
        {
            _axisClass = FLIPPED_AXIS_CLASS;
        }
    }

    async Task ShowUpdate(Phase phase)
    {
        var model = new PhaseUpdateModel(phase);
        await Dialog.ShowUpdateForm(model);
        await InvokeRender();
    }
}
