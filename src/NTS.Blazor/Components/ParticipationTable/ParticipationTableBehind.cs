using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using Not.Krud.Blazor;
using NTS.Application.Core;
using NTS.Blazor.Components.ParticipationTable.Phases;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Blazor.Components.ParticipationTable;

public class ParticipationTableBehind : NComponent
{
    const string FLIPPED_AXIS_CLASS = "rtable--flip";

    [Inject]
    KrudDialogService<PhaseUpdateModel, PhaseUpdateShell> Dialog { get; set; } = default!;

    protected string AxisClass { get; private set; } = FLIPPED_AXIS_CLASS;

    protected bool AnyRepresentation { get; private set; }

    protected bool AnyRequiredInspection { get; private set; }

    protected bool AnyCompulsoryRequiredInspection { get; private set; }

    [Parameter]
    public PhaseCollection? Phases { get; set; }

    [Parameter]
    public bool Editable { get; set; } = true;

    [Parameter]
    public bool AlignVertically { get; set; }

    protected override void OnParametersSet()
    {
        AnyRepresentation = Phases != null && Phases.Any(x => x.IsReinspectionRequested);
        AnyRequiredInspection = Phases != null && Phases.Any(x => x.IsRequiredInspectionRequested);
        AnyCompulsoryRequiredInspection = Phases != null && Phases.Any(x => x.IsRequiredInspectionCompulsory);
        FlipAxis();
    }

    protected void FlipAxis()
    {
        if (AlignVertically)
        {
            AxisClass = "size-xs";
        }
        else
        {
            AxisClass = FLIPPED_AXIS_CLASS;
        }
    }

    protected async Task ShowUpdate(Phase phase)
    {
        try
        {
            var model = new PhaseUpdateModel(phase);
            await Dialog.ShowUpdateForm(model);
            await InvokeRender();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
